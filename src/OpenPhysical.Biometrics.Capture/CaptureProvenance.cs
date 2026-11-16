// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>Immutable acquisition provenance, distinct from record certification.</summary>
public sealed record CaptureProvenance
{
    private CaptureProvenance(
        CaptureDeviceDescriptor device,
        string version,
        CaptureGeometry raw,
        CaptureGeometry output,
        CaptureSettings settings,
        ImmutableArray<byte> digest,
        string correction,
        CaptureResolution resolution,
        CaptureQualification qualification
    )
    {
        Device = device;
        DriverVersion = version;
        RawGeometry = raw;
        OutputGeometry = output;
        Settings = settings;
        CalibrationDigest = digest;
        CorrectionVersion = correction;
        Resolution = resolution;
        Qualification = qualification;
    }

    /// <summary>Gets the device metadata.</summary>
    public CaptureDeviceDescriptor Device { get; }

    /// <summary>Gets the driver version.</summary>
    public string DriverVersion { get; }

    /// <summary>Gets raw sensor geometry.</summary>
    public CaptureGeometry RawGeometry { get; }

    /// <summary>Gets delivered image geometry.</summary>
    public CaptureGeometry OutputGeometry { get; }

    /// <summary>Gets the applied settings.</summary>
    public CaptureSettings Settings { get; }

    /// <summary>Gets the SHA-256 calibration digest, empty for raw images.</summary>
    public ImmutableArray<byte> CalibrationDigest { get; }

    /// <summary>Gets correction implementation version, empty for raw images.</summary>
    public string CorrectionVersion { get; }

    /// <summary>Gets physical-scale evidence.</summary>
    public CaptureResolution Resolution { get; }

    /// <summary>Gets the qualification assertion.</summary>
    public CaptureQualification Qualification { get; }

    /// <summary>Validates provenance before binding it to pixels.</summary>
    /// <param name="device">Capture device.</param>
    /// <param name="version">Driver version.</param>
    /// <param name="raw">Sensor geometry.</param>
    /// <param name="output">Delivered geometry.</param>
    /// <param name="settings">Applied settings.</param>
    /// <param name="digest">Calibration SHA-256 bytes or empty for raw acquisition.</param>
    /// <param name="correction">Correction version or empty for raw acquisition.</param>
    /// <param name="resolution">Scale evidence.</param>
    /// <param name="qualification">Qualification assertion.</param>
    /// <returns>Provenance or calibration failure.</returns>
    public static Result<CaptureProvenance, CaptureError> Create(
        CaptureDeviceDescriptor device,
        string version,
        CaptureGeometry raw,
        CaptureGeometry output,
        CaptureSettings settings,
        ReadOnlySpan<byte> digest,
        string correction,
        CaptureResolution resolution,
        CaptureQualification qualification
    )
    {
        var metadata = ValidateMetadata(
            device,
            version,
            raw,
            output,
            settings,
            resolution,
            qualification
        );
        if (metadata.IsFailure)
            return metadata.Error;
        if (!ValidCorrection(settings.Corrected, digest, correction, raw, output))
            return new CaptureError(
                CaptureErrorCodes.CalibrationInvalid,
                "Raw and corrected provenance are inconsistent."
            );
        return new CaptureProvenance(
            device,
            version,
            raw,
            output,
            settings,
            ImmutableArray.Create(digest.ToArray()),
            correction,
            resolution,
            qualification
        );
    }

    private static UnitResult<CaptureError> ValidateMetadata(
        CaptureDeviceDescriptor device,
        string version,
        CaptureGeometry raw,
        CaptureGeometry output,
        CaptureSettings settings,
        CaptureResolution resolution,
        CaptureQualification qualification
    )
    {
        if (
            device is null
            || raw is null
            || output is null
            || settings is null
            || resolution is null
            || qualification is null
            || !CaptureDeviceDescriptor.ValidText(version)
        )
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.CalibrationInvalid,
                    "Required acquisition metadata is missing."
                )
            );
        if (
            device.Capabilities.Validate(settings).IsFailure
            || (
                qualification.IsQualified
                && resolution.Kind != CaptureResolution.EvidenceKind.Measured
            )
        )
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.CalibrationInvalid,
                    "Settings or qualification evidence is inconsistent."
                )
            );
        return UnitResult.Success<CaptureError>();
    }

    private static bool ValidCorrection(
        bool corrected,
        ReadOnlySpan<byte> digest,
        string version,
        CaptureGeometry raw,
        CaptureGeometry output
    ) =>
        corrected
            ? digest.Length == 32 && CaptureDeviceDescriptor.ValidText(version)
            : digest.IsEmpty && version == string.Empty && raw == output;
}
