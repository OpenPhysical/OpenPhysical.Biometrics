// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Inspects raw biometric objects using the canonical codecs and PIV profiles.</summary>
/// <remarks>Inspection always yields a report. Structural failures appear as located diagnostics in the report.</remarks>
public static class BiometricInspector
{
    /// <summary>Identifies, parses, and inspects one raw biometric object without sensitive field values.</summary>
    /// <param name="data">Raw FMR, FAC, or CBEFF bytes supplied after card-object unwrapping.</param>
    /// <param name="evaluationTime">Explicit instant used for CBEFF validity checks.</param>
    /// <returns>Structural locations, metadata summaries, and independent PIV profile findings.</returns>
    public static BiometricInspectionReport Inspect(
        ReadOnlySpan<byte> data,
        DateTimeOffset evaluationTime
    ) => Inspect(data, evaluationTime, new InspectionOptions());

    /// <summary>Identifies, parses, and inspects one raw biometric object.</summary>
    /// <param name="data">Raw FMR, FAC, or CBEFF bytes supplied after card-object unwrapping.</param>
    /// <param name="evaluationTime">Explicit instant used for CBEFF validity checks.</param>
    /// <param name="options">Explicit opt-in for sensitive field contents.</param>
    /// <returns>Structural locations, metadata summaries, and independent PIV profile findings.</returns>
    public static BiometricInspectionReport Inspect(
        ReadOnlySpan<byte> data,
        DateTimeOffset evaluationTime,
        InspectionOptions options
    ) => InspectCore(data, evaluationTime, options, allowCbeff: true);

    private static BiometricInspectionReport InspectCore(
        ReadOnlySpan<byte> data,
        DateTimeOffset evaluationTime,
        InspectionOptions options,
        bool allowCbeff
    )
    {
        if (data.StartsWith("FMR\0"u8))
            return InspectFingerprints(data, options);
        if (data.StartsWith("FAC\0"u8))
            return InspectFace(data, options);
        return allowCbeff
            ? InspectCbeff(data, evaluationTime, options)
            : Unstructured(
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
                )
            );
    }

    private static BiometricInspectionReport InspectFingerprints(
        ReadOnlySpan<byte> data,
        InspectionOptions options
    ) =>
        Incits378Codec
            .Parse(data, options)
            .Match(
                parsed => new BiometricInspectionReport(
                    BiometricRecordKind.Fingerprints,
                    parsed.Inspection,
                    PivBiometricProfile.Validate(parsed.Record),
                    parsed
                        .Record.Views.Select(view => new FingerViewSummary(
                            view.FingerPosition,
                            view.ViewNumber,
                            view.Minutiae.Length
                        ))
                        .ToImmutableArray(),
                    [],
                    false
                ),
                error => Unstructured(BiometricRecordKind.Fingerprints, error.Inspection)
            );

    private static BiometricInspectionReport InspectFace(
        ReadOnlySpan<byte> data,
        InspectionOptions options
    ) =>
        Incits385Codec
            .Parse(data, options)
            .Match(
                parsed => new BiometricInspectionReport(
                    BiometricRecordKind.FacialImages,
                    parsed.Inspection,
                    PivBiometricProfile.Validate(parsed.Record),
                    [],
                    parsed
                        .Record.Images.Select(image => new FacialImageSummary(
                            image.ImageInformation.Width,
                            image.ImageInformation.Height,
                            image.ImageInformation.ImageDataType
                        ))
                        .ToImmutableArray(),
                    false
                ),
                error => Unstructured(BiometricRecordKind.FacialImages, error.Inspection)
            );

    private static BiometricInspectionReport InspectCbeff(
        ReadOnlySpan<byte> data,
        DateTimeOffset evaluationTime,
        InspectionOptions options
    )
    {
        var cbeff = CbeffCodec.Parse(data, options);
        if (cbeff.IsFailure)
            return Unstructured(BiometricRecordKind.Unknown, cbeff.Error.Inspection);
        var wrapped = cbeff.Value.Record;
        var child = InspectCore(
            wrapped.BiometricData.AsSpan(),
            evaluationTime,
            options,
            allowCbeff: false
        );
        return new(
            BiometricRecordKind.Cbeff,
            Merge(cbeff.Value.Inspection, child.Structure),
            PivBiometricProfile.Validate(wrapped, evaluationTime),
            child.FingerViews,
            child.FacialImages,
            !wrapped.Signature.IsEmpty
        );
    }

    // Child offsets are relative to the biometric block; report them in the wrapper's buffer.
    private static InspectionReport Merge(InspectionReport wrapper, InspectionReport child) =>
        new(
            wrapper.Fields.AddRange(
                child.Fields.Select(field =>
                    field with
                    {
                        Name = "BiometricData." + field.Name,
                        Offset = checked(field.Offset + CbeffHeader.Size),
                    }
                )
            ),
            wrapper.Diagnostics.AddRange(
                child.Diagnostics.Select(diagnostic =>
                    diagnostic with
                    {
                        Offset = checked(diagnostic.Offset + CbeffHeader.Size),
                    }
                )
            )
        );

    private static BiometricInspectionReport Unstructured(
        BiometricRecordKind kind,
        InspectionReport structure
    ) => new(kind, structure, [], [], [], false);
}
