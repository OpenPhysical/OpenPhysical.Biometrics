// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

internal sealed record TiffLayout(
    int Width,
    int Height,
    int Samples,
    uint Photometric,
    uint RowsPerStrip,
    uint[] Offsets,
    uint[] Lengths
)
{
    internal static Result<TiffLayout, ImagingError> Read(TiffDirectory directory) =>
        directory
            .Scalar(256)
            .Bind(width => directory.Scalar(257).Map(height => (Width: width, Height: height)))
            .Ensure(
                size => RgbImage.DimensionsValid(size.Width, size.Height),
                ImagingErrors.InvalidDimensions()
            )
            .Bind(size =>
                ReadModel(directory)
                    .Bind(model =>
                        ReadStrips(directory, size.Height)
                            .Map(strips => new TiffLayout(
                                (int)size.Width,
                                (int)size.Height,
                                model.Samples,
                                model.Photometric,
                                strips.RowsPerStrip,
                                strips.Offsets,
                                strips.Lengths
                            ))
                    )
            );

    internal bool StripsFit(int dataLength)
    {
        var y = 0;
        for (var strip = 0; strip < Offsets.Length; strip++)
        {
            var rows = (int)Math.Min(RowsPerStrip, (uint)(Height - y));
            if (
                Lengths[strip] != (long)rows * Width * Samples
                || (ulong)Offsets[strip] + Lengths[strip] > (ulong)dataLength
            )
                return false;
            y += rows;
        }
        return true;
    }

    private static Result<(int Samples, uint Photometric), ImagingError> ReadModel(
        TiffDirectory directory
    ) =>
        directory
            .Scalar(277, 1)
            .Ensure(
                samples => samples is 1 or 3 or 4,
                Unsupported(
                    "TIFF decoding requires uncompressed, top-left, interleaved grayscale or RGB pixels."
                )
            )
            .Bind(samples => RequireOne(directory, 259).Map(() => (int)samples))
            .Bind(samples => RequireOne(directory, 284).Map(() => samples))
            .Bind(samples => RequireOne(directory, 274).Map(() => samples))
            .Bind(samples =>
                directory
                    .Scalar(262)
                    .Ensure(
                        photometric => samples == 1 ? photometric is 0 or 1 : photometric == 2,
                        Unsupported("The TIFF colour model is unsupported.")
                    )
                    .Bind(photometric =>
                        RequireStraightAlpha(directory, samples).Map(() => photometric)
                    )
                    .Bind(photometric =>
                        RequireEightBit(directory, samples).Map(() => (samples, photometric))
                    )
            );

    private static UnitResult<ImagingError> RequireOne(TiffDirectory directory, ushort tag) =>
        directory
            .Scalar(tag, 1)
            .Ensure(
                value => value == 1,
                Unsupported(
                    "TIFF decoding requires uncompressed, top-left, interleaved grayscale or RGB pixels."
                )
            )
            .AsUnit();

    // RgbaPixel stores straight colour channels. Associated or unspecified extra
    // samples would change their meaning, so require an explicit unassociated alpha tag.
    private static UnitResult<ImagingError> RequireStraightAlpha(
        TiffDirectory directory,
        int samples
    ) =>
        samples != 4
            ? UnitResult.Success<ImagingError>()
            : directory
                .Scalar(338, 0)
                .Ensure(
                    value => value == 2,
                    Unsupported("RGBA TIFF decoding requires explicit unassociated alpha.")
                )
                .AsUnit();

    private static UnitResult<ImagingError> RequireEightBit(TiffDirectory directory, int samples) =>
        directory
            .Values(258)
            .Ensure(
                bits => bits.Length == samples && bits.All(value => value == 8),
                Unsupported("TIFF decoding requires eight-bit samples.")
            )
            .AsUnit();

    private static Result<
        (uint RowsPerStrip, uint[] Offsets, uint[] Lengths),
        ImagingError
    > ReadStrips(TiffDirectory directory, uint height) =>
        directory
            .Values(273)
            .Bind(offsets =>
                directory
                    .Values(279)
                    .Bind(lengths =>
                        directory
                            .Scalar(278, height)
                            .Map(rows => (RowsPerStrip: rows, Offsets: offsets, Lengths: lengths))
                    )
            )
            .Ensure(
                strips =>
                    strips.RowsPerStrip != 0
                    && strips.RowsPerStrip <= int.MaxValue
                    && strips.Offsets.Length != 0
                    && strips.Offsets.Length == strips.Lengths.Length
                    && (ulong)strips.Offsets.Length
                        == ((ulong)height + strips.RowsPerStrip - 1) / strips.RowsPerStrip,
                ImagingErrors.TiffMalformed("The TIFF strip layout is invalid.")
            );

    private static ImagingError Unsupported(string detail) => ImagingErrors.TiffUnsupported(detail);
}
