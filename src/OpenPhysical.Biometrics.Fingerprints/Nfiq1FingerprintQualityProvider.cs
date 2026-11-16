// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ1;

/// <summary>Adapts the original NIST NFIQ 1 classifier to PIV enrollment.</summary>
public sealed class Nfiq1FingerprintQualityProvider : IFingerprintQualityProvider
{
    private readonly INfiq1Engine engine;

    /// <summary>Creates the provider with an explicitly selected NFIQ 1 engine.</summary>
    /// <param name="engine">The original five-level NFIQ 1 classifier.</param>
    public Nfiq1FingerprintQualityProvider(INfiq1Engine engine)
    {
        this.engine = engine;
    }

    /// <summary>Assesses the original capture and preserves the NIST classification details.</summary>
    /// <param name="image">The immutable nominal 500 ppi parent image.</param>
    /// <returns>The original quality class, confidence, image status, and model identity, or an error when the engine fails or returns an unknown image status.</returns>
    public Result<FingerprintQualityAssessment, FingerprintError> Assess(FingerprintImage image) =>
        FingerprintEdge
            .Try(
                () => engine.Assess(image.Pixels, image.Width, image.Height, image.DotsPerInch),
                FingerprintErrorCodes.QualityEngineFailed,
                "The quality engine failed."
            )
            .Bind(assessment =>
                ToStatus(assessment.Status)
                    .Bind(status =>
                        FingerprintQualityAssessment.Create(
                            assessment.QualityClass,
                            "NFIQ 1; NBIS 5.0.0 default weights",
                            Maybe.From((double)assessment.Confidence),
                            status
                        )
                    )
            );

    private static Result<FingerprintQualityStatus, FingerprintError> ToStatus(
        Nfiq1AssessmentStatus status
    ) =>
        status switch
        {
            Nfiq1AssessmentStatus.Assessed => FingerprintQualityStatus.Assessed,
            Nfiq1AssessmentStatus.EmptyImage => FingerprintQualityStatus.EmptyImage,
            Nfiq1AssessmentStatus.TooFewMinutiae => FingerprintQualityStatus.InsufficientMinutiae,
            _ => FingerprintChecks.Fail<FingerprintQualityStatus>(
                FingerprintErrorCodes.UnknownQualityStatus,
                "The quality provider returned an unknown image status."
            ),
        };
}
