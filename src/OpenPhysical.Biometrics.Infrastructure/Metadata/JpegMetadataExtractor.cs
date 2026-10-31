using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions.Metadata;
using OpenPhysical.Domains.Biometrics;
using ImageDomains = OpenPhysical.Domains.Images;
using MetadataDirectory = MetadataExtractor.Directory;

namespace OpenPhysical.Biometrics.Infrastructure.Metadata;

/// <summary>Reads JPEG and EXIF metadata directly from the original encoded bytes.</summary>
/// <remarks>Metadata is read before image decoding, so camera tags and GPS values remain available even when a processing backend discards them.</remarks>
public sealed class JpegMetadataExtractor : IJpegMetadataExtractor
{
    private readonly ILogger<JpegMetadataExtractor> _logger;

    /// <summary>Gets the JPEG format supported by this extractor.</summary>
    public ImmutableHashSet<ImageDomains.ImageFormat> SupportedFormats { get; } =
        ImmutableHashSet.Create(ImageDomains.ImageFormat.Jpeg);

    /// <summary>Creates a JPEG metadata extractor.</summary>
    /// <param name="logger">Operational diagnostics for metadata-reading failures.</param>
    public JpegMetadataExtractor(ILogger<JpegMetadataExtractor> logger) => _logger = logger;

    /// <summary>Checks whether the supplied bytes start with a JPEG marker.</summary>
    /// <param name="imageData">Original encoded image bytes.</param>
    /// <returns>Whether the data has the JPEG start-of-image marker.</returns>
    public Result<bool> CanExtract(ImmutableArray<byte> imageData) =>
        Result.Success(
            !imageData.IsDefaultOrEmpty
                && imageData.Length >= 3
                && imageData[0] == 0xff
                && imageData[1] == 0xd8
                && imageData[2] == 0xff
        );

    /// <summary>Reads general image metadata asynchronously.</summary>
    /// <param name="imageData">Original JPEG bytes.</param>
    /// <returns>JPEG dimensions, pixel information and available EXIF data.</returns>
    public Task<Result<ImageDomains.ImageMetadata>> ExtractAsync(ImmutableArray<byte> imageData) =>
        Task.Run(() => Extract(imageData));

    /// <summary>Reads general image metadata.</summary>
    /// <param name="imageData">Original JPEG bytes.</param>
    /// <returns>JPEG dimensions, pixel information and available EXIF data.</returns>
    public Result<ImageDomains.ImageMetadata> Extract(ImmutableArray<byte> imageData) =>
        ExtractJpegMetadata(imageData).Map(jpeg => jpeg.BaseMetadata);

    Task<Result<ImageDomains.JpegMetadata>> IJpegMetadataExtractor.ExtractAsync(
        ImmutableArray<byte> imageData
    ) => Task.Run(() => ExtractJpegMetadata(imageData));

    Result<ImageDomains.JpegMetadata> IJpegMetadataExtractor.Extract(
        ImmutableArray<byte> imageData
    ) => ExtractJpegMetadata(imageData);

    /// <summary>Reads camera, capture-time and location tags from the original JPEG.</summary>
    /// <param name="imageData">Original JPEG bytes, including its EXIF segment.</param>
    /// <returns>The available EXIF values, or an empty value when the JPEG has no EXIF profile.</returns>
    public Result<ImageDomains.ExifData> ExtractExif(ImmutableArray<byte> imageData)
    {
        try
        {
            return ParseExif(ReadDirectories(imageData));
        }
        catch (Exception)
        {
            _logger.LogError("JPEG EXIF metadata could not be read.");
            return Result.Failure<ImageDomains.ExifData>("JPEG EXIF metadata could not be read.");
        }
    }

    private Result<ImageDomains.JpegMetadata> ExtractJpegMetadata(ImmutableArray<byte> imageData)
    {
        try
        {
            var directories = ReadDirectories(imageData);
            var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();
            if (jpeg is null)
                return Result.Failure<ImageDomains.JpegMetadata>("JPEG frame metadata is missing.");
            var dimensions = ImageDimensions.Create(
                checked((ushort)jpeg.GetImageWidth()),
                checked((ushort)jpeg.GetImageHeight())
            );
            if (dimensions.IsFailure)
                return Result.Failure<ImageDomains.JpegMetadata>(dimensions.Error);
            var exif = Maybe<ImageDomains.ExifData>.From(ParseExif(directories));
            int components = jpeg.GetNumberOfComponents();
            int precision = jpeg.GetInt32(JpegDirectory.TagDataPrecision);
            var color = components switch
            {
                1 => "Grayscale",
                3 => "YCbCr",
                4 => "CMYK",
                _ => "Unknown",
            };
            var baseMetadata = ImageDomains.ImageMetadata.Create(
                ImageDomains.ImageFormat.Jpeg,
                dimensions.Value,
                exif.Bind(value => value.DateTimeOriginal),
                exif.Bind(value => value.DateTime),
                Maybe<int>.From(precision * components),
                Maybe<string>.From(color),
                Maybe<string>.From("JPEG"),
                exif,
                ImmutableDictionary<string, object>.Empty
            );
            if (baseMetadata.IsFailure)
                return Result.Failure<ImageDomains.JpegMetadata>(baseMetadata.Error);
            return ImageDomains.JpegMetadata.Create(
                baseMetadata.Value,
                EstimateJpegQuality(imageData.AsSpan()),
                Maybe<string>.None,
                jpeg.ContainsTag(JpegDirectory.TagCompressionType)
                    && jpeg.GetInt32(JpegDirectory.TagCompressionType) == 2
            );
        }
        catch (Exception)
        {
            _logger.LogError("JPEG metadata could not be read.");
            return Result.Failure<ImageDomains.JpegMetadata>("JPEG metadata could not be read.");
        }
    }

    private static IReadOnlyList<MetadataDirectory> ReadDirectories(ImmutableArray<byte> imageData)
    {
        using var stream = new MemoryStream(imageData.ToArray(), writable: false);
        return ImageMetadataReader.ReadMetadata(stream);
    }

    private static ImageDomains.ExifData ParseExif(IReadOnlyList<MetadataDirectory> directories)
    {
        var exif = directories
            .Where(directory => directory is ExifIfd0Directory or ExifSubIfdDirectory)
            .ToArray();
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        if (exif.Length == 0 && gps is null)
            return ImageDomains.ExifData.Empty;
        object? Value(int tag) =>
            exif.Select(directory => directory.GetObject(tag))
                .FirstOrDefault(value => value is not null);
        Maybe<string> Text(int tag) =>
            Value(tag)?.ToString() is { Length: > 0 } text
                ? Maybe<string>.From(text)
                : Maybe<string>.None;
        Maybe<double> RationalValue(int tag) =>
            Value(tag) is Rational rational
                ? Maybe<double>.From(rational.ToDouble())
                : Maybe<double>.None;
        Maybe<int> Integer(int tag)
        {
            var directory = exif.FirstOrDefault(item => item.ContainsTag(tag));
            return directory is not null && directory.TryGetInt32(tag, out int value)
                ? Maybe<int>.From(value)
                : Maybe<int>.None;
        }
        Maybe<DateTime> Date(int tag)
        {
            var directory = exif.FirstOrDefault(item => item.ContainsTag(tag));
            return directory is not null && directory.TryGetDateTime(tag, out var value)
                ? Maybe<DateTime>.From(value)
                : Maybe<DateTime>.None;
        }
        var tags = ImmutableDictionary.CreateBuilder<ImageDomains.ExifTag, object>();
        foreach (
            var directory in exif.Cast<MetadataDirectory>()
                .Concat(gps is null ? [] : new MetadataDirectory[] { gps })
        )
        foreach (var tag in directory.Tags)
        {
            var name = new string(tag.Name.Where(char.IsLetterOrDigit).ToArray());
            if (Enum.TryParse<ImageDomains.ExifTag>(name, ignoreCase: true, out var mapped))
                tags[mapped] = directory.GetObject(tag.Type) ?? string.Empty;
        }
        return ImageDomains.ExifData.Create(
            Text(0x010f),
            Text(0x0110),
            Date(0x9003),
            Date(0x0132),
            Text(0x0131),
            Text(0x010e),
            Text(0x013b),
            Text(0x8298),
            ReadGps(gps),
            RationalValue(0x829a),
            RationalValue(0x829d),
            Integer(0x8827),
            RationalValue(0x920a),
            Integer(0x0112),
            tags.ToImmutable()
        );
    }

    private static Maybe<ImageDomains.GpsCoordinates> ReadGps(GpsDirectory? gps)
    {
        if (
            gps?.GetObject(GpsDirectory.TagLatitude) is not Rational[] latitude
            || latitude.Length < 3
            || gps.GetObject(GpsDirectory.TagLongitude) is not Rational[] longitude
            || longitude.Length < 3
        )
            return Maybe<ImageDomains.GpsCoordinates>.None;
        var latitudeRef = gps.GetString(GpsDirectory.TagLatitudeRef);
        var longitudeRef = gps.GetString(GpsDirectory.TagLongitudeRef);
        if (latitudeRef is null || longitudeRef is null)
            return Maybe<ImageDomains.GpsCoordinates>.None;
        var altitude = gps.GetObject(GpsDirectory.TagAltitude) is Rational value
            ? Maybe<double>.From(
                value.ToDouble()
                    * (gps.GetObject(GpsDirectory.TagAltitudeRef) is byte b && b == 1 ? -1 : 1)
            )
            : Maybe<double>.None;
        var result = ImageDomains.GpsCoordinates.CreateFromDms(
            latitude[0].ToDouble(),
            latitude[1].ToDouble(),
            latitude[2].ToDouble(),
            latitudeRef,
            longitude[0].ToDouble(),
            longitude[1].ToDouble(),
            longitude[2].ToDouble(),
            longitudeRef,
            altitude
        );
        return result.IsSuccess
            ? Maybe<ImageDomains.GpsCoordinates>.From(result.Value)
            : Maybe<ImageDomains.GpsCoordinates>.None;
    }

    // JPEG stores quantization tables rather than an encoder quality setting.
    // Report a quality only when the luminance table exactly matches an IJG table.
    private static Maybe<int> EstimateJpegQuality(ReadOnlySpan<byte> jpeg)
    {
        ReadOnlySpan<int> natural =
        [
            16,
            11,
            10,
            16,
            24,
            40,
            51,
            61,
            12,
            12,
            14,
            19,
            26,
            58,
            60,
            55,
            14,
            13,
            16,
            24,
            40,
            57,
            69,
            56,
            14,
            17,
            22,
            29,
            51,
            87,
            80,
            62,
            18,
            22,
            37,
            56,
            68,
            109,
            103,
            77,
            24,
            35,
            55,
            64,
            81,
            104,
            113,
            92,
            49,
            64,
            78,
            87,
            103,
            121,
            120,
            101,
            72,
            92,
            95,
            98,
            112,
            100,
            103,
            99,
        ];
        ReadOnlySpan<int> zigzag =
        [
            0,
            1,
            8,
            16,
            9,
            2,
            3,
            10,
            17,
            24,
            32,
            25,
            18,
            11,
            4,
            5,
            12,
            19,
            26,
            33,
            40,
            48,
            41,
            34,
            27,
            20,
            13,
            6,
            7,
            14,
            21,
            28,
            35,
            42,
            49,
            56,
            57,
            50,
            43,
            36,
            29,
            22,
            15,
            23,
            30,
            37,
            44,
            51,
            58,
            59,
            52,
            45,
            38,
            31,
            39,
            46,
            53,
            60,
            61,
            54,
            47,
            55,
            62,
            63,
        ];
        int offset = 2;
        while (offset + 4 <= jpeg.Length)
        {
            if (jpeg[offset++] != 0xff)
                return Maybe<int>.None;
            while (offset < jpeg.Length && jpeg[offset] == 0xff)
                offset++;
            if (offset >= jpeg.Length)
                return Maybe<int>.None;
            byte marker = jpeg[offset++];
            if (marker is 0xda or 0xd9)
                break;
            if (offset + 2 > jpeg.Length)
                break;
            int length = (jpeg[offset] << 8) | jpeg[offset + 1];
            if (length < 2 || length > jpeg.Length - offset)
                break;
            if (marker == 0xdb)
            {
                int cursor = offset + 2;
                while (cursor < offset + length)
                {
                    byte table = jpeg[cursor++];
                    int size = (table >> 4) == 0 ? 64 : 128;
                    if (size > offset + length - cursor)
                        return Maybe<int>.None;
                    if (table == 0)
                    {
                        for (int quality = 1; quality <= 100; quality++)
                        {
                            int scale = quality < 50 ? 5000 / quality : 200 - 2 * quality;
                            bool matches = true;
                            for (int index = 0; index < 64; index++)
                                if (
                                    jpeg[cursor + index]
                                    != Math.Clamp(
                                        (natural[zigzag[index]] * scale + 50) / 100,
                                        1,
                                        255
                                    )
                                )
                                {
                                    matches = false;
                                    break;
                                }
                            if (matches)
                                return Maybe<int>.From(quality);
                        }
                        return Maybe<int>.None;
                    }
                    cursor += size;
                }
            }
            offset += length;
        }
        return Maybe<int>.None;
    }
}
