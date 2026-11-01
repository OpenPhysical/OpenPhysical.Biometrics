// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

internal static class BaselineTiffDecoder
{
    internal static Result<RgbImage, ImagingError> Decode(ReadOnlyMemory<byte> data) =>
        TiffDirectory
            .Parse(data)
            .Bind(directory =>
                TiffLayout
                    .Read(directory)
                    .Ensure(
                        layout => layout.StripsFit(directory.Length),
                        ImagingErrors.TiffMalformed("A TIFF strip has inconsistent bounds.")
                    )
                    .Map(layout => Build(data.Span, layout))
            );

    private static RgbImage Build(ReadOnlySpan<byte> data, TiffLayout layout)
    {
        var pixels = new RgbaPixel[layout.Width * layout.Height];
        var y = 0;
        for (var strip = 0; strip < layout.Offsets.Length; strip++)
        {
            var rows = (int)Math.Min(layout.RowsPerStrip, (uint)(layout.Height - y));
            ReadRows(data, layout, pixels, (int)layout.Offsets[strip], y, rows);
            y += rows;
        }
        return RgbImage.Adopt(pixels, layout.Width, layout.Height);
    }

    private static RgbaPixel[] ReadRows(
        ReadOnlySpan<byte> data,
        TiffLayout layout,
        RgbaPixel[] pixels,
        int position,
        int firstRow,
        int rows
    )
    {
        var samples = layout.Samples;
        var index = firstRow * layout.Width;
        for (var count = layout.Width * rows; count > 0; count--, index++, position += samples)
            pixels[index] =
                samples == 1
                    ? Gray(data[position], layout.Photometric)
                    : new(
                        data[position],
                        data[position + 1],
                        data[position + 2],
                        samples == 4 ? data[position + 3] : (byte)255
                    );
        return pixels;
    }

    private static RgbaPixel Gray(byte sample, uint photometric) =>
        new(photometric == 0 ? (byte)(255 - sample) : sample);
}
