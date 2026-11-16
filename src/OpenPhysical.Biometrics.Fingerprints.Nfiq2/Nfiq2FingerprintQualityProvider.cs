// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ2;

/// <summary>Adapts a reusable NFIQ 2 engine to supplemental continuous image quality.</summary>
/// <remarks>This provider does not supply PIV quality or replace the original NFIQ 1 classifier.</remarks>
public sealed class Nfiq2FingerprintQualityProvider : IFingerprintUnifiedQualityProvider
{
    private readonly INfiq2Engine engine;

    /// <summary>Creates a provider that borrows a reusable, externally owned native engine.</summary>
    /// <param name="engine">The engine whose model and native lifetime are managed by the caller.</param>
    public Nfiq2FingerprintQualityProvider(INfiq2Engine engine)
    {
        this.engine = engine;
    }

    /// <summary>Computes the unified score on the exact supplied grayscale image.</summary>
    /// <param name="image">An optical or scanned-ink plain fingerprint at nominal 500 ppi.</param>
    /// <returns>A continuous score with model identity, or a typed image or operational failure.</returns>
    public Result<FingerprintUnifiedQualityAssessment, FingerprintError> Assess(
        FingerprintImage image
    ) =>
        Nfiq2QualityEdge
            .Assess(() => engine.Assess(image.Pixels, image.Width, image.Height, image.DotsPerInch))
            .Bind(assessment =>
                FingerprintUnifiedQualityAssessment.Create(
                    assessment.UnifiedQualityScore,
                    "NFIQ 2.3.0; embedded optical/ink model"
                )
            );
}
