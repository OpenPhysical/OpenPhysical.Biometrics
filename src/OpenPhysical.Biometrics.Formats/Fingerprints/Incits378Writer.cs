// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

// Serializes a validated record. Lengths and counts derive from the immutable collections.
internal static class Incits378Writer
{
    private static readonly byte[] Signature = "FMR\0 20\0"u8.ToArray();

    internal static byte[] Serialize(FingerprintRecord record)
    {
        var body = record.Views.Aggregate(WriteHeader(record), WriteView);
        // The length field counts the identifier, version, and itself.
        var shortLength = body.Length + 10;
        var framed = new BinaryFormatWriter().Bytes(Signature);
        return (
            shortLength <= ushort.MaxValue
                ? framed.UInt16((ushort)shortLength)
                : framed.UInt16(0).UInt32((uint)(shortLength + 4))
        )
            .Bytes(body.ToArray())
            .ToArray();
    }

    private static BinaryFormatWriter WriteHeader(FingerprintRecord record) =>
        new BinaryFormatWriter()
            .UInt16(record.ProductOwner)
            .UInt16(record.ProductType)
            .UInt16(record.CaptureEquipment)
            .UInt16(record.Width)
            .UInt16(record.Height)
            .UInt16(record.XResolution)
            .UInt16(record.YResolution)
            .Byte((byte)record.Views.Length)
            .Byte(0);

    private static BinaryFormatWriter WriteView(BinaryFormatWriter body, FingerprintView view)
    {
        var header = body.Byte(view.FingerPosition)
            .Byte((byte)(view.ViewNumber << 4 | view.ImpressionType))
            .Byte(view.Quality)
            .Byte((byte)view.Minutiae.Length);
        var withMinutiae = view.Minutiae.Aggregate(
            header,
            (writer, point) =>
                writer
                    .UInt16((ushort)(point.Type << 14 | point.X))
                    .UInt16(point.Y)
                    .Byte(point.Angle)
                    .Byte(point.Quality)
        );
        return view.ExtendedBlocks.Aggregate(
            withMinutiae.UInt16((ushort)Incits378Validator.ExtendedLength(view)),
            (writer, block) =>
                writer.UInt16(block.Type).UInt16((ushort)(block.Data.Length + 4)).Bytes(block.Data)
        );
    }
}
