// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Validated immutable FS50 contact and reliable-area policy.</summary>
public sealed record Fs50EvidencePolicy
{
    /// <summary>Current evidence policy canonicalization and algorithm identifier.</summary>
    public const string Version = "fs50-phase-aligned-contact-v5";

    private Fs50EvidencePolicy(Fs50EvidenceOptions options)
    {
        Options = options;
        Digest = Fs50PolicyDigest.Create(Version, options);
    }

    /// <summary>Gets the validated default policy.</summary>
    public static Fs50EvidencePolicy Default { get; } = Create(new Fs50EvidenceOptions()).Value;

    /// <summary>Gets immutable validated policy settings.</summary>
    public Fs50EvidenceOptions Options { get; }

    /// <summary>Gets version-bound SHA-256 of the complete policy.</summary>
    public string Digest { get; }

    /// <summary>Validates finite bounded detection and mask settings.</summary>
    /// <param name="options">Immutable proposed settings.</param>
    /// <returns>A validated policy or invalid-argument failure.</returns>
    public static Result<Fs50EvidencePolicy, CaptureError> Create(Fs50EvidenceOptions options) =>
        options is not null && ValidTexture(options) && ValidRidges(options)
            ? new Fs50EvidencePolicy(options)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "FS50 evidence settings are outside their validated bounds."
            );

    private static bool ValidTexture(Fs50EvidenceOptions o) =>
        Finite(o.TextureVariance, 0, 16000)
        && Finite(o.FaintVariance, 0, o.TextureVariance)
        && Finite(o.TextureMean, 0, 255)
        && Finite(o.FaintMean, o.TextureMean, 255)
        && o.MinimumArea is >= 256 and <= 600000
        && o.MinimumWidth is >= 8 and <= 800
        && o.MinimumHeight is >= 8 and <= 750
        && Finite(o.MinimumSignal, 0, 255)
        && o.ClosingRadius is >= 0 and <= 32
        && o.OpeningRadius is >= 0 and <= 32
        && o.SupportedExpansion is >= 0 and <= 64
        && o.RidgeBoundaryTolerance is >= 0 and <= 64
        && o.InnerGuard is >= 0 and <= 32;

    private static bool ValidRidges(Fs50EvidenceOptions o) =>
        Finite(o.MinimumCoherence, 0, 1)
        && Finite(o.MinimumBlockCoverage, 0.25, 1)
        && Finite(o.MinimumProfileContrast, 0, 255)
        && o.MinimumTransitions is >= 2 and <= 16
        && o.MinimumPeriod is >= 2 and <= 16
        && o.MaximumPeriod >= o.MinimumPeriod
        && o.MaximumPeriod <= 24
        && Finite(o.MinimumPeriodCorrelation, 0, 1)
        && Finite(o.MaximumHalfPeriodCorrelation, -1, 1)
        && Finite(o.MinimumParallelCorrelation, 0, 1)
        && o.MaximumParallelShift is >= 0 and <= 8
        && o.MinimumSupportedBlocks is >= 1 and <= 25
        && o.PeriodTolerance is >= 0 and <= 16;

    private static bool Finite(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;
}
