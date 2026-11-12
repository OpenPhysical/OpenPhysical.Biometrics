// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using CSharpFunctionalExtensions;

/// <summary>Validated application rules for tolerant presentations and bounded candidate retention.</summary>
public sealed record FingerCapturePolicy
{
    private FingerCapturePolicy(
        int candidateLimit,
        TimeSpan candidateWindow,
        TimeSpan acquisitionGap,
        double majorDisplacementFraction,
        double minimumOverlap,
        int absenceFrames,
        int repositionFrames
    )
    {
        CandidateLimit = candidateLimit;
        CandidateWindow = candidateWindow;
        AcquisitionGap = acquisitionGap;
        MajorDisplacementFraction = majorDisplacementFraction;
        MinimumOverlap = minimumOverlap;
        AbsenceFrames = absenceFrames;
        RepositionFrames = repositionFrames;
    }

    /// <summary>Gets the approved five-frame, 750-millisecond tolerant capture policy.</summary>
    public static FingerCapturePolicy Default { get; } =
        new(5, TimeSpan.FromMilliseconds(750), TimeSpan.FromSeconds(1), 0.2, 0.5, 2, 2);

    /// <summary>Gets the bounded number of retained images.</summary>
    public int CandidateLimit { get; }

    /// <summary>Gets the maximum candidate age at the capture deadline.</summary>
    public TimeSpan CandidateWindow { get; }

    /// <summary>Gets the acquisition gap that clears a presentation.</summary>
    public TimeSpan AcquisitionGap { get; }

    /// <summary>Gets the permitted displacement relative to the initial region diagonal.</summary>
    public double MajorDisplacementFraction { get; }

    /// <summary>Gets the required intersection-over-union before reposition debounce.</summary>
    public double MinimumOverlap { get; }

    /// <summary>Gets the consecutive empty observations required to acknowledge removal.</summary>
    public int AbsenceFrames { get; }

    /// <summary>Gets the consecutive major displacements required to restart timing.</summary>
    public int RepositionFrames { get; }

    /// <summary>Validates policy values while preserving the five-image memory and scoring bound.</summary>
    /// <param name="candidateLimit">One through five retained images.</param>
    /// <param name="candidateWindow">Positive age through 750 milliseconds.</param>
    /// <param name="acquisitionGap">Positive stale-observation interval through ten seconds.</param>
    /// <param name="majorDisplacementFraction">Finite displacement fraction greater than zero through one.</param>
    /// <param name="minimumOverlap">Finite overlap fraction greater than zero through one.</param>
    /// <param name="absenceFrames">One through ten consecutive empty observations.</param>
    /// <param name="repositionFrames">One through ten consecutive major displacements.</param>
    /// <returns>A validated policy or invalid-argument error.</returns>
    public static Result<FingerCapturePolicy, CaptureError> Create(
        int candidateLimit,
        TimeSpan candidateWindow,
        TimeSpan acquisitionGap,
        double majorDisplacementFraction,
        double minimumOverlap,
        int absenceFrames,
        int repositionFrames
    ) =>
        candidateLimit is >= 1 and <= 5
        && candidateWindow > TimeSpan.Zero
        && candidateWindow <= TimeSpan.FromMilliseconds(750)
        && acquisitionGap > TimeSpan.Zero
        && acquisitionGap <= TimeSpan.FromSeconds(10)
        && double.IsFinite(majorDisplacementFraction)
        && majorDisplacementFraction is > 0 and <= 1
        && double.IsFinite(minimumOverlap)
        && minimumOverlap is > 0 and <= 1
        && absenceFrames is >= 1 and <= 10
        && repositionFrames is >= 1 and <= 10
            ? new FingerCapturePolicy(
                candidateLimit,
                candidateWindow,
                acquisitionGap,
                majorDisplacementFraction,
                minimumOverlap,
                absenceFrames,
                repositionFrames
            )
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Capture policy values exceed their validated bounds."
            );
}
