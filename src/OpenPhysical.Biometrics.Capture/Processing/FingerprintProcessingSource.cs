// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using CSharpFunctionalExtensions;

/// <summary>Validated acquisition identity fields needed to verify saved processing evidence.</summary>
public sealed record FingerprintProcessingSource
{
    private FingerprintProcessingSource(
        string deviceId,
        long sequence,
        DateTimeOffset capturedAt,
        int exposure,
        string correctionVersion,
        string calibrationDigest
    )
    {
        DeviceId = deviceId;
        Sequence = sequence;
        CapturedAt = capturedAt.ToUniversalTime();
        Exposure = exposure;
        CorrectionVersion = correctionVersion;
        CalibrationDigest = calibrationDigest.ToLowerInvariant();
    }

    /// <summary>Gets the acquisition device identifier.</summary>
    public string DeviceId { get; }

    /// <summary>Gets the session frame sequence.</summary>
    public long Sequence { get; }

    /// <summary>Gets the acquisition instant in UTC.</summary>
    public DateTimeOffset CapturedAt { get; }

    /// <summary>Gets the applied sensor exposure.</summary>
    public int Exposure { get; }

    /// <summary>Gets the correction implementation identifier, empty for raw acquisition.</summary>
    public string CorrectionVersion { get; }

    /// <summary>Gets the lowercase calibration SHA-256, empty for raw acquisition.</summary>
    public string CalibrationDigest { get; }

    /// <summary>Validates saved source fields without reconstructing or processing an image.</summary>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="sequence">Nonnegative session sequence.</param>
    /// <param name="capturedAt">Acquisition instant.</param>
    /// <param name="exposure">Applied exposure from zero through 255.</param>
    /// <param name="correctionVersion">Correction version or empty for raw samples.</param>
    /// <param name="calibrationDigest">SHA-256 hex or empty for raw samples.</param>
    /// <returns>Validated source identity or invalid-argument failure.</returns>
    public static Result<FingerprintProcessingSource, CaptureError> Create(
        string deviceId,
        long sequence,
        DateTimeOffset capturedAt,
        int exposure,
        string correctionVersion,
        string calibrationDigest
    ) =>
        CaptureDeviceDescriptor.ValidText(deviceId)
        && sequence >= 0
        && capturedAt != default
        && exposure is >= 0 and <= 255
        && correctionVersion is not null
        && calibrationDigest is not null
        && (
            (correctionVersion.Length == 0 && calibrationDigest.Length == 0)
            || (
                CaptureDeviceDescriptor.ValidText(correctionVersion)
                && FingerprintPreprocessingIdentity.ValidDigest(calibrationDigest)
            )
        )
            ? new FingerprintProcessingSource(
                deviceId,
                sequence,
                capturedAt,
                exposure,
                correctionVersion,
                calibrationDigest
            )
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Saved processing source identity is invalid."
            );

    internal static FingerprintProcessingSource From(CaptureFrame frame) =>
        Create(
            frame.Provenance.Device.DeviceId,
            frame.Sequence,
            frame.CapturedAt,
            frame.Provenance.Settings.Exposure,
            frame.Provenance.CorrectionVersion,
            Convert.ToHexStringLower(frame.Provenance.CalibrationDigest.AsSpan())
        ).Value;
}
