// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Serialises <see cref="TiffSpec"/> values into TIFF bytes.</summary>
internal static class TiffFixtures
{
    /// <summary>Builds a one-pixel little-endian TIFF with ascending tags.</summary>
    /// <param name="spec">Description of the TIFF contents.</param>
    /// <returns>The TIFF bytes.</returns>
    internal static byte[] Build(TiffSpec spec)
    {
        var count = spec.ExtraSample.HasValue ? 12 : 11;
        var bitsOffset = 8 + 2 + count * 12 + 4;
        var pixelOffset = bitsOffset + (spec.Samples == 1 ? 0 : spec.Samples * 2);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[] { 0x49, 0x49, 42, 0 });
        writer.Write(8u);
        writer.Write((ushort)count);
        Entry(writer, 256, 4, 1, 1);
        Entry(writer, 257, 4, 1, 1);
        Entry(writer, 258, 3, spec.Samples, spec.Samples == 1 ? 8u : (uint)bitsOffset);
        Entry(writer, 259, 3, 1, spec.Compression);
        Entry(writer, 262, 3, 1, spec.Photometric);
        Entry(writer, 273, 4, 1, (uint)pixelOffset);
        Entry(writer, 274, 3, 1, 1);
        Entry(writer, 277, 3, 1, spec.Samples);
        Entry(writer, 278, 4, 1, 1);
        Entry(writer, 279, 4, 1, spec.StripLength.GetValueOrDefault((uint)spec.Pixels.Length));
        Entry(writer, 284, 3, 1, 1);
        if (spec.ExtraSample.HasValue)
            Entry(writer, 338, 3, 1, spec.ExtraSample.Value);
        writer.Write(0u);
        if (spec.Samples > 1)
            for (var channel = 0; channel < spec.Samples; channel++)
                writer.Write((ushort)8);
        writer.Write(spec.Pixels);
        return stream.ToArray();
    }

    private static void Entry(BinaryWriter writer, ushort tag, ushort type, uint values, uint value)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(values);
        writer.Write(value);
    }
}
