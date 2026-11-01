// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Applies the SP 800-76-2 PIV record profiles independently of cryptographic verification.</summary>
public static class PivBiometricProfile
{
    /// <summary>Checks the fingerprint record fields required for new PIV issuance.</summary>
    /// <param name="record">The canonical parsed or prepared record.</param>
    /// <returns>All detected profile errors, with no biometric field contents.</returns>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(FingerprintRecord record) =>
        PivFingerprintProfile.Validate(record);

    /// <summary>Checks the PIV fingerprint header independently of the number of views.</summary>
    /// <param name="record">A canonical record carrying the original header fields.</param>
    /// <returns>Profile findings for the supplied header.</returns>
    public static ImmutableArray<BiometricProfileDiagnostic> ValidateFingerprintHeader(
        FingerprintRecord record
    ) => PivFingerprintProfile.ValidateHeader(record);

    /// <summary>Checks the intrinsic PIV requirements of one selected finger view.</summary>
    /// <param name="view">The canonical view with explicit quality and availability values.</param>
    /// <returns>View profile findings; source-geometry checks remain part of full record validation.</returns>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(FingerprintView view) =>
        PivFingerprintProfile.ValidateView(view);

    /// <summary>Checks the encoded facial record metadata required by the PIV profile.</summary>
    /// <param name="record">The canonical facial record.</param>
    /// <returns>Metadata/profile errors, separate from acquisition and image-processing assessments.</returns>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(FaceRecord record) =>
        PivFacialProfile.Validate(record);

    /// <summary>Checks common signed PIV header fields and the enclosed biometric profile.</summary>
    /// <param name="record">The raw CBEFF record.</param>
    /// <param name="evaluationTime">The explicit instant for validity evaluation.</param>
    /// <returns>Header and biometric profile findings, independently of signature-service findings.</returns>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(
        CbeffRecord record,
        DateTimeOffset evaluationTime
    ) => PivCbeffProfile.Validate(record, evaluationTime);
}
