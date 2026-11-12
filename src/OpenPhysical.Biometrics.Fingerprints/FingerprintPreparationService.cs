// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;

/// <summary>Assesses and extracts an image once, independently of capture-equipment metadata.</summary>
public sealed class FingerprintPreparationService
{
    private readonly IFingerprintEngine engine;
    private readonly IFingerprintQualityProvider qualityProvider;

    /// <summary>Creates shared image processing with explicit extraction and quality providers.</summary>
    /// <param name="engine">The reusable NBIS extraction and comparison engine.</param>
    /// <param name="qualityProvider">The parent-image NFIQ 1 provider.</param>
    public FingerprintPreparationService(
        IFingerprintEngine engine,
        IFingerprintQualityProvider qualityProvider
    )
    {
        this.engine = engine;
        this.qualityProvider = qualityProvider;
    }

    /// <summary>Assesses and extracts the exact immutable image once for subsequent reuse.</summary>
    /// <param name="image">Top-left-origin Gray8 image with explicit nominal 500 ppi resolution.</param>
    /// <returns>The image and immutable processing results, or a quality/extraction failure.</returns>
    public Result<PreparedFingerprint, FingerprintError> Prepare(FingerprintImage image) =>
        Assess(image).Bind(Extract);

    /// <summary>Assesses the exact image without running minutia extraction.</summary>
    /// <param name="image">Immutable nominal 500 ppi grayscale image.</param>
    /// <returns>The image bound to its reusable NFIQ 1 assessment, or a quality-provider failure.</returns>
    public Result<AssessedFingerprint, FingerprintError> Assess(FingerprintImage image) =>
        qualityProvider.Assess(image).Map(quality => new AssessedFingerprint(image, quality));

    /// <summary>Extracts one assessed image while reusing its existing quality result.</summary>
    /// <param name="assessed">The exact image and assessment selected for extraction.</param>
    /// <returns>Reusable extraction and the unchanged assessment, or an extraction failure.</returns>
    public Result<PreparedFingerprint, FingerprintError> Extract(AssessedFingerprint assessed) =>
        FingerprintEdge
            .Try(
                () => engine.Extract(assessed.Image),
                FingerprintErrorCodes.EngineFailed,
                "The fingerprint engine failed."
            )
            .Map(template => new PreparedFingerprint(assessed.Image, template, assessed.Quality));
}
