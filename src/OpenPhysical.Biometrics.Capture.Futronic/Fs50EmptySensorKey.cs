// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Complete compatibility identity for a RAM-only corrected empty-sensor reference.</summary>
public sealed record Fs50EmptySensorKey
{
    private Fs50EmptySensorKey(
        string deviceId,
        string sessionId,
        string calibrationDigest,
        string correctionVersion,
        string driverVersion,
        CaptureGeometry geometry,
        int exposure
    )
    {
        DeviceId = deviceId;
        SessionId = sessionId;
        CalibrationDigest = calibrationDigest;
        CorrectionVersion = correctionVersion;
        DriverVersion = driverVersion;
        Width = geometry.Width;
        Height = geometry.Height;
        Exposure = (byte)exposure;
    }

    /// <summary>Gets the device identifier.</summary>
    public string DeviceId { get; }

    /// <summary>Gets the caller's current acquisition-session identity.</summary>
    public string SessionId { get; }

    /// <summary>Gets the calibration SHA-256.</summary>
    public string CalibrationDigest { get; }

    /// <summary>Gets the correction implementation.</summary>
    public string CorrectionVersion { get; }

    /// <summary>Gets the acquisition driver version.</summary>
    public string DriverVersion { get; }

    /// <summary>Gets corrected image width.</summary>
    public int Width { get; }

    /// <summary>Gets corrected image height.</summary>
    public int Height { get; }

    /// <summary>Gets actual sensor exposure.</summary>
    public byte Exposure { get; }

    /// <summary>Gets versioned metadata identity without granting any capture authorization.</summary>
    public string Digest =>
        Fs50PolicyDigest.Create(
            "fs50-whole-contact-session-key-v1",
            new
            {
                DeviceId,
                SessionId,
                CalibrationDigest,
                CorrectionVersion,
                DriverVersion,
                Width,
                Height,
                Exposure,
            }
        );

    /// <summary>Validates exact FS50 geometry, corrected stage and explicit session identity.</summary>
    /// <param name="frame">Current acquisition.</param>
    /// <param name="sessionId">Nonempty stable identity for this open session.</param>
    /// <returns>The complete compatibility key or precise validation failure.</returns>
    public static Result<Fs50EmptySensorKey, CaptureError> Create(
        CaptureFrame frame,
        string sessionId
    ) =>
        frame is not null
        && frame.Width == 800
        && frame.Height == 750
        && frame.Stage == CaptureFrame.FrameStage.Corrected
        && !string.IsNullOrWhiteSpace(sessionId)
        && sessionId.Length <= 256
        && !sessionId.Any(char.IsControl)
            ? Create(
                frame.Provenance.Device.DeviceId,
                sessionId,
                Convert.ToHexStringLower(frame.Provenance.CalibrationDigest.AsSpan()),
                frame.Provenance.CorrectionVersion,
                frame.Provenance.DriverVersion,
                frame.Provenance.OutputGeometry,
                frame.Provenance.Settings.Exposure
            )
            : new CaptureError(
                CaptureErrorCodes.InvalidFrame,
                "Empty-sensor learning requires a corrected FS50 frame and explicit session identity."
            );

    /// <summary>Validates saved acquisition metadata without images or live qualification.</summary>
    /// <param name="deviceId">Original acquisition device.</param>
    /// <param name="sessionId">Original open-session identity.</param>
    /// <param name="calibrationDigest">Corrected-source calibration SHA-256.</param>
    /// <param name="correctionVersion">Original correction implementation.</param>
    /// <param name="driverVersion">Original acquisition driver.</param>
    /// <param name="geometry">Original corrected geometry.</param>
    /// <param name="exposure">Original applied exposure.</param>
    /// <returns>Validated metadata identity, never a live capture proof.</returns>
    public static Result<Fs50EmptySensorKey, CaptureError> Create(
        string deviceId,
        string sessionId,
        string calibrationDigest,
        string correctionVersion,
        string driverVersion,
        CaptureGeometry geometry,
        int exposure
    ) =>
        ValidIdentityText(deviceId)
        && ValidIdentityText(correctionVersion)
        && ValidIdentityText(driverVersion)
        && Fs50PolicyDigest.IsDigest(calibrationDigest)
        && !string.IsNullOrWhiteSpace(sessionId)
        && sessionId.Length <= 256
        && !sessionId.Any(char.IsControl)
        && geometry is not null
        && geometry.Width == 800
        && geometry.Height == 750
        && exposure is >= 0 and <= 255
            ? new Fs50EmptySensorKey(
                deviceId,
                sessionId,
                calibrationDigest,
                correctionVersion,
                driverVersion,
                geometry,
                exposure
            )
            : new CaptureError(
                CaptureErrorCodes.InvalidFrame,
                "Saved empty-reference acquisition key is invalid."
            );

    private static bool ValidIdentityText(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && !value.Any(char.IsControl);

    internal bool SameSession(Fs50EmptySensorKey other) =>
        DeviceId == other.DeviceId
        && SessionId == other.SessionId
        && CalibrationDigest == other.CalibrationDigest
        && CorrectionVersion == other.CorrectionVersion
        && DriverVersion == other.DriverVersion
        && Width == other.Width
        && Height == other.Height;
}
