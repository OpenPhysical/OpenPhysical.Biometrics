// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Identifies the binary record interpreted by inspection.</summary>
public enum BiometricRecordKind
{
    /// <summary>The bytes have an unknown record format.</summary>
    Unknown,

    /// <summary>A raw INCITS 378-2004 fingerprint record.</summary>
    Fingerprints,

    /// <summary>A raw INCITS 385-2004 facial image record.</summary>
    FacialImages,

    /// <summary>A PIV CBEFF header, biometric record, and detached security block.</summary>
    Cbeff,
}

/// <summary>Describes the selected finger and view metadata.</summary>
/// <param name="FingerPosition">Encoded finger position.</param>
/// <param name="ViewNumber">Encoded view selection.</param>
/// <param name="MinutiaCount">Number of encoded minutiae.</param>
public sealed record FingerViewSummary(byte FingerPosition, byte ViewNumber, int MinutiaCount);

/// <summary>Describes facial image dimensions and encoding metadata.</summary>
/// <param name="Width">Encoded width in pixels.</param>
/// <param name="Height">Encoded height in pixels.</param>
/// <param name="ImageDataType">The encoded image compression type.</param>
public sealed record FacialImageSummary(ushort Width, ushort Height, byte ImageDataType);

/// <summary>Provides shared inspection data for CLI, middleware, and application renderers.</summary>
/// <param name="Kind">The identified record kind.</param>
/// <param name="Structure">Located structural fields and diagnostics.</param>
/// <param name="ProfileDiagnostics">PIV profile findings, independent of external cryptographic findings.</param>
/// <param name="FingerViews">Selections and counts for fingerprint records.</param>
/// <param name="FacialImages">Geometry and encoding for facial records.</param>
/// <param name="HasSignatureBlock">Whether the CBEFF object contains a detached security block.</param>
public sealed record BiometricInspectionReport(
    BiometricRecordKind Kind,
    InspectionReport Structure,
    ImmutableArray<BiometricProfileDiagnostic> ProfileDiagnostics,
    ImmutableArray<FingerViewSummary> FingerViews,
    ImmutableArray<FacialImageSummary> FacialImages,
    bool HasSignatureBlock
);

/// <summary>Inspects raw biometric objects using the canonical codecs and PIV profiles.</summary>
public static class BiometricInspector
{
    /// <summary>Identifies, parses, and inspects one raw biometric object.</summary>
    /// <param name="data">Raw FMR, FAC, or CBEFF bytes supplied after card-object unwrapping.</param>
    /// <param name="evaluationTime">Explicit instant used for CBEFF validity checks.</param>
    /// <param name="options">Explicit opt-in for sensitive field contents.</param>
    /// <returns>Structural locations, metadata summaries, and independent PIV profile findings.</returns>
    public static BiometricInspectionReport Inspect(
        ReadOnlySpan<byte> data,
        DateTimeOffset evaluationTime,
        InspectionOptions? options = null
    ) => InspectCore(data, evaluationTime, options, allowCbeff: true);

    private static BiometricInspectionReport InspectCore(
        ReadOnlySpan<byte> data,
        DateTimeOffset evaluationTime,
        InspectionOptions? options,
        bool allowCbeff
    )
    {
        if (data.StartsWith("FMR\0"u8))
        {
            var parsed = Incits378Codec.Parse(data, options);
            return new(
                BiometricRecordKind.Fingerprints,
                parsed.Inspection,
                parsed.Value is { } record ? PivBiometricProfile.Validate(record) : [],
                Summarize(parsed.Value),
                [],
                false
            );
        }
        if (data.StartsWith("FAC\0"u8))
        {
            var parsed = Incits385Codec.Parse(data, options);
            return new(
                BiometricRecordKind.FacialImages,
                parsed.Inspection,
                parsed.Value is { } record ? PivBiometricProfile.Validate(record) : [],
                [],
                Summarize(parsed.Value),
                false
            );
        }
        if (!allowCbeff)
            return new(
                BiometricRecordKind.Unknown,
                new(
                    [],
                    [
                        new(
                            "BiometricFormat",
                            "Expected a fingerprint or facial record.",
                            0,
                            Math.Min(4, data.Length)
                        ),
                    ]
                ),
                [],
                [],
                [],
                false
            );
        var cbeff = CbeffCodec.Parse(data, options);
        if (cbeff.Value is not { } wrapped)
            return new(BiometricRecordKind.Unknown, cbeff.Inspection, [], [], [], false);
        var child = InspectCore(
            wrapped.BiometricData.AsSpan(),
            evaluationTime,
            options,
            allowCbeff: false
        );
        var fields = cbeff.Inspection.Fields.AddRange(
            child.Structure.Fields.Select(field =>
                field with
                {
                    Name = "BiometricData." + field.Name,
                    Offset = checked(field.Offset + CbeffHeader.Size),
                }
            )
        );
        var diagnostics = cbeff.Diagnostics.AddRange(
            child.Structure.Diagnostics.Select(diagnostic =>
                diagnostic with
                {
                    Offset = checked(diagnostic.Offset + CbeffHeader.Size),
                }
            )
        );
        return new(
            BiometricRecordKind.Cbeff,
            new(fields, diagnostics),
            PivBiometricProfile.Validate(wrapped, evaluationTime),
            child.FingerViews,
            child.FacialImages,
            !wrapped.Signature.IsEmpty
        );
    }

    private static ImmutableArray<FingerViewSummary> Summarize(FingerprintRecord? record) =>
        record is null
            ? []
            : record
                .Views.Select(view => new FingerViewSummary(
                    view.FingerPosition,
                    view.ViewNumber,
                    view.Minutiae.Length
                ))
                .ToImmutableArray();

    private static ImmutableArray<FacialImageSummary> Summarize(FaceRecord? record) =>
        record is null
            ? []
            : record
                .Images.Select(image => new FacialImageSummary(
                    image.ImageInformation.Width,
                    image.ImageInformation.Height,
                    image.ImageInformation.ImageDataType
                ))
                .ToImmutableArray();
}
