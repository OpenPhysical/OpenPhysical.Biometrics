namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using Canonical = OpenPhysical.Biometrics.Formats.Fingerprints;

// Framing and validation belong to the canonical codec. This adapter retains
// the typed extension objects used by existing inspection callers.
internal static class LegacyFingerprintProjection
{
    internal static Canonical.FingerprintRecord ToCanonical(FingerprintRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var header = record.Header;
        return new(
            header.ProductOwner,
            header.ProductType,
            header.CaptureEquipmentCompliance,
            header.ImageSizeX,
            header.ImageSizeY,
            header.XResolution,
            header.YResolution,
            record.ViewRecords.Select(ToCanonical).ToImmutableArray()
        );
    }

    /// <summary>Projects one compatibility view into canonical minutiae and extended payloads.</summary>
    /// <param name="view">The middleware compatibility view.</param>
    /// <returns>The canonical view with original ordering.</returns>
    /// <exception cref="ArgumentNullException">The view is null.</exception>
    internal static Canonical.FingerprintView ToCanonical(FingerViewRecord view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new(
            view.FingerPosition,
            view.ViewNumber,
            view.ImpressionType,
            view.FingerQuality,
            view.Minutiae.Select(point => new Canonical.FingerprintMinutia(
                    (byte)point.MinutiaType,
                    point.XCoordinate,
                    point.YCoordinate,
                    point.Angle,
                    point.Quality
                ))
                .ToImmutableArray(),
            view.ExtendedData.Select(ToCanonical).ToImmutableArray()
        );
    }

    /// <summary>Creates a compatibility projection after canonical structural validation.</summary>
    /// <param name="record">The authoritative fingerprint record.</param>
    /// <returns>A compatibility record retaining all extended payload bytes.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    /// <exception cref="ArgumentException">The record is structurally invalid.</exception>
    internal static FingerprintRecord FromCanonical(Canonical.FingerprintRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var encoded = Canonical.Incits378Codec.Write(record);
        var header = GeneralRecordHeader
            .Create(
                [0x46, 0x4d, 0x52, 0],
                [0x20, 0x32, 0x30, 0],
                (uint)encoded.Length,
                record.ProductOwner,
                record.ProductType,
                record.CaptureEquipment,
                record.Width,
                record.Height,
                record.XResolution,
                record.YResolution,
                checked((byte)record.Views.Length),
                0
            )
            .Value;
        var views = record
            .Views.Select(view =>
                FingerViewRecord
                    .Create(
                        view.FingerPosition,
                        view.ViewNumber,
                        view.ImpressionType,
                        view.Quality,
                        checked((byte)view.Minutiae.Length),
                        view.Minutiae.Select(point =>
                                MinutiaPoint
                                    .Create(
                                        point.Type,
                                        point.X,
                                        point.Y,
                                        point.Angle,
                                        point.Quality
                                    )
                                    .Value
                            )
                            .ToImmutableArray(),
                        view.ExtendedBlocks.Select(ProjectExtension).ToImmutableArray()
                    )
                    .Value
            )
            .ToImmutableArray();
        return new(header, views);
    }

    private static ExtendedDataBlock ProjectExtension(Canonical.FingerprintExtendedBlock block)
    {
        var data = block.Data;
        var offset = 0;
        ushort Read16()
        {
            var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(
                data.AsSpan(offset, 2)
            );
            offset += 2;
            return value;
        }
        if (block.Type == 1)
        {
            var method = (RidgeCountExtractionMethod)data[offset++];
            var entries = ImmutableArray.CreateBuilder<RidgeCountEntry>();
            while (offset < data.Length)
            {
                entries.Add(
                    RidgeCountEntry.CreateForParsing(
                        data[offset],
                        data[offset + 1],
                        data[offset + 2]
                    )
                );
                offset += 3;
            }
            return RidgeCountData.Create(method, entries.ToImmutable()).Value;
        }
        if (block.Type == 2)
        {
            var coreInfo = data[offset++];
            var coreType = (CoreInformationType)(coreInfo >> 6);
            var cores = ImmutableArray.CreateBuilder<CorePoint>();
            for (var index = 0; index < (coreInfo & 15); index++)
            {
                var x = (ushort)(Read16() & 0x3fff);
                var y = (ushort)(Read16() & 0x3fff);
                byte? angle = coreType == CoreInformationType.HasAngle ? data[offset++] : null;
                cores.Add(CorePoint.CreateForParsing(x, y, angle));
            }
            var deltaInfo = data[offset++];
            var deltaType = (DeltaInformationType)(deltaInfo >> 6);
            var deltas = ImmutableArray.CreateBuilder<DeltaPoint>();
            for (var index = 0; index < (deltaInfo & 15); index++)
            {
                var x = (ushort)(Read16() & 0x3fff);
                var y = (ushort)(Read16() & 0x3fff);
                ImmutableArray<byte>? angles = null;
                if (deltaType == DeltaInformationType.HasAngles)
                {
                    angles = data.AsSpan(offset, 3).ToArray().ToImmutableArray();
                    offset += 3;
                }
                deltas.Add(DeltaPoint.CreateForParsing(x, y, angles));
            }
            return CoreDeltaData
                .Create(coreType, cores.ToImmutable(), deltaType, deltas.ToImmutable())
                .Value;
        }
        // Existing vendor models expose the first two payload bytes as the vendor
        // identifier. Opaque shorter payloads retain all bytes in an internal block.
        return data.Length >= 2
            ? VendorData.CreateForParsing(
                block.Type,
                Read16(),
                data.AsSpan()[2..].ToArray().ToImmutableArray()
            )
            : new OpaqueExtension(block.Type, data);
    }

    private static Canonical.FingerprintExtendedBlock ToCanonical(ExtendedDataBlock block)
    {
        if (block is OpaqueExtension raw)
            return new(raw.TypeIdentificationCode, raw.Payload);
        var data = new List<byte>();
        void Write16(ushort value)
        {
            data.Add((byte)(value >> 8));
            data.Add((byte)value);
        }
        switch (block)
        {
            case VendorData vendor:
                Write16(vendor.VendorCode);
                data.AddRange(vendor.Data);
                break;
            case RidgeCountData ridge:
                data.Add((byte)ridge.ExtractionMethod);
                foreach (var entry in ridge.RidgeCounts)
                    data.AddRange(
                        new[]
                        {
                            entry.FirstMinutiaIndex,
                            entry.SecondMinutiaIndex,
                            entry.RidgeCount,
                        }
                    );
                break;
            case CoreDeltaData geometry:
                data.Add((byte)(((byte)geometry.CoreInfoType << 6) | geometry.Cores.Length));
                foreach (var point in geometry.Cores)
                {
                    Write16(point.XCoordinate);
                    Write16(point.YCoordinate);
                    if (geometry.CoreInfoType == CoreInformationType.HasAngle)
                        data.Add(point.Angle.Value);
                }
                data.Add((byte)(((byte)geometry.DeltaInfoType << 6) | geometry.Deltas.Length));
                foreach (var point in geometry.Deltas)
                {
                    Write16(point.XCoordinate);
                    Write16(point.YCoordinate);
                    if (geometry.DeltaInfoType == DeltaInformationType.HasAngles)
                        data.AddRange(point.Angles.Value);
                }
                break;
            default:
                throw new ArgumentException(
                    "The extended block has no canonical projection.",
                    nameof(block)
                );
        }
        return new(block.TypeIdentificationCode, data.ToImmutableArray());
    }

    private sealed record OpaqueExtension : ExtendedDataBlock
    {
        internal OpaqueExtension(ushort type, ImmutableArray<byte> payload)
            : base(type, checked((ushort)(payload.Length + 4))) => Payload = payload;

        internal ImmutableArray<byte> Payload { get; }

        public override Result ValidateCompliance() => Result.Success();
    }
}
