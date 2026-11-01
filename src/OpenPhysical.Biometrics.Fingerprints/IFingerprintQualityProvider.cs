// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

/// <summary>Supplies the NFIQ 1 assessment used in PIV enrollment.</summary>
public interface IFingerprintQualityProvider
{
    /// <summary>Assesses a captured image on the original NFIQ 1 scale.</summary>
    /// <param name="capture">The immutable capture to assess.</param>
    /// <returns>The NFIQ 1 class and provider identity, or an error when assessment fails.</returns>
    Result<FingerprintQualityAssessment, FingerprintError> Assess(FingerprintCapture capture);
}
