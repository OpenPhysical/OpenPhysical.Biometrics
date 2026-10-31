// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using OpenPhysical.Biometrics.NFIQ1;

/// <summary>Adapts the original NIST NFIQ 1 classifier to PIV enrollment.</summary>
public sealed class Nfiq1FingerprintQualityProvider : IFingerprintQualityProvider
{
    private readonly INfiq1Engine engine;

    /// <summary>Creates the provider with an explicitly selected NFIQ 1 engine.</summary>
    /// <param name="engine">The original five-level NFIQ 1 classifier.</param>
    /// <exception cref="ArgumentNullException">The engine is null.</exception>
    public Nfiq1FingerprintQualityProvider(INfiq1Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        this.engine = engine;
    }

    /// <summary>Assesses the original capture and preserves the NIST classification details.</summary>
    /// <param name="capture">The immutable parent image.</param>
    /// <returns>The original quality class, confidence, image status, and model identity.</returns>
    /// <exception cref="ArgumentNullException">The capture is null.</exception>
    /// <exception cref="InvalidOperationException">The engine returns an unknown assessment status.</exception>
    public FingerprintQualityAssessment Assess(FingerprintCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var image = capture.Image;
        var assessment = engine.Assess(image.Pixels, image.Width, image.Height, image.DotsPerInch);
        var status = assessment.Status switch
        {
            Nfiq1AssessmentStatus.Assessed => FingerprintQualityStatus.Assessed,
            Nfiq1AssessmentStatus.EmptyImage => FingerprintQualityStatus.EmptyImage,
            Nfiq1AssessmentStatus.TooFewMinutiae => FingerprintQualityStatus.InsufficientMinutiae,
            _ => throw new InvalidOperationException(
                "The quality provider returned an unknown image status."
            ),
        };
        return new(
            assessment.QualityClass,
            "NFIQ 1; NBIS 5.0.0 default weights",
            assessment.Confidence,
            status
        );
    }
}
