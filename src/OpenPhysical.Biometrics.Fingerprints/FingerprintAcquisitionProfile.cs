// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

/// <summary>Explicit equipment mapping from externally approved acquisition policy, not self-issued certification.</summary>
public sealed record FingerprintAcquisitionProfile
{
    private FingerprintAcquisitionProfile(
        string id,
        string deviceId,
        string driverVersion,
        string correctionVersion,
        ImmutableArray<byte> calibrationDigest,
        string qualificationId,
        ushort equipmentId,
        bool appendixFCertified,
        CaptureGeometry outputGeometry
    )
    {
        OutputGeometry = outputGeometry;
        Id = id;
        DeviceId = deviceId;
        DriverVersion = driverVersion;
        CorrectionVersion = correctionVersion;
        CalibrationDigest = calibrationDigest;
        QualificationId = qualificationId;
        EquipmentId = equipmentId;
        AppendixFCertified = appendixFCertified;
    }

    /// <summary>Gets the qualified corrected output dimensions.</summary>
    public CaptureGeometry OutputGeometry { get; }

    /// <summary>Gets the approved profile identity.</summary>
    public string Id { get; }

    /// <summary>Gets the specific qualified device identity.</summary>
    public string DeviceId { get; }

    /// <summary>Gets the qualified driver version.</summary>
    public string DriverVersion { get; }

    /// <summary>Gets the qualified correction version.</summary>
    public string CorrectionVersion { get; }

    /// <summary>Gets the expected calibration SHA-256 digest.</summary>
    public ImmutableArray<byte> CalibrationDigest { get; }

    /// <summary>Gets the external qualification reference.</summary>
    public string QualificationId { get; }

    /// <summary>Gets the registered equipment identifier supplied by approved policy.</summary>
    public ushort EquipmentId { get; }

    /// <summary>Gets the equipment certification assertion in approved policy.</summary>
    public bool AppendixFCertified { get; }

    /// <summary>Validates a profile without manufacturing absent equipment or qualification evidence.</summary>
    /// <param name="id">Approved profile identity.</param>
    /// <param name="deviceId">Qualified device identity.</param>
    /// <param name="driverVersion">Driver version.</param>
    /// <param name="correctionVersion">Correction version.</param>
    /// <param name="calibrationDigest">Calibration SHA-256.</param>
    /// <param name="qualificationId">External qualification reference.</param>
    /// <param name="equipmentId">Registered equipment identifier.</param>
    /// <param name="appendixFCertified">Certification assertion from the approved profile.</param>
    /// <param name="outputGeometry">Qualified corrected output dimensions.</param>
    /// <returns>A profile or a typed metadata failure.</returns>
    public static Result<FingerprintAcquisitionProfile, FingerprintError> Create(
        string id,
        string deviceId,
        string driverVersion,
        string correctionVersion,
        ReadOnlySpan<byte> calibrationDigest,
        string qualificationId,
        ushort equipmentId,
        bool appendixFCertified,
        CaptureGeometry outputGeometry
    )
    {
        var valid = ValidateMetadata(
            id,
            deviceId,
            driverVersion,
            correctionVersion,
            calibrationDigest.Length,
            qualificationId,
            equipmentId,
            outputGeometry
        );
        if (valid.IsFailure)
            return valid.Error;
        return new FingerprintAcquisitionProfile(
            id,
            deviceId,
            driverVersion,
            correctionVersion,
            ImmutableArray.Create(calibrationDigest.ToArray()),
            qualificationId,
            equipmentId,
            appendixFCertified,
            outputGeometry
        );
    }

    private static UnitResult<FingerprintError> ValidateMetadata(
        string id,
        string deviceId,
        string driverVersion,
        string correctionVersion,
        int digestLength,
        string qualificationId,
        ushort equipmentId,
        CaptureGeometry outputGeometry
    )
    {
        if (
            outputGeometry is null
            || !ValidText(id)
            || !ValidText(deviceId)
            || !ValidText(driverVersion)
            || !ValidText(correctionVersion)
            || !ValidText(qualificationId)
            || digestLength != 32
        )
            return UnitResult.Failure(
                new FingerprintError(
                    FingerprintErrorCodes.AcquisitionProfileMismatch,
                    "Acquisition profile metadata is invalid."
                )
            );
        if (equipmentId is < 1 or > 4095)
            return UnitResult.Failure(
                new FingerprintError(
                    FingerprintErrorCodes.InvalidEquipment,
                    "An explicit registered equipment identifier is required."
                )
            );
        return UnitResult.Success<FingerprintError>();
    }

    private static bool ValidText(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && !value.Any(char.IsControl);
}
