// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Actual pixel context, spatial feature admission and artificial-cut guard for one processing choice.</summary>
public sealed class Fs50ContourProcessingRegion
{
    private Fs50ContourProcessingRegion(FingerprintProcessingMask mask, byte[] guarded, int guard)
    {
        Mask = mask;
        GuardedContextMask = [.. guarded];
        GuardedContextDigest = Fs50PolicyDigest.Pixels(guarded);
        AppliedGuard = guard;
    }

    /// <summary>Gets Retained equal to E and Inner equal to F intersected with guarded E.</summary>
    public FingerprintProcessingMask Mask { get; }

    /// <summary>Gets E after excluding only within-image artificial cuts.</summary>
    public ImmutableArray<byte> GuardedContextMask { get; }

    /// <summary>Gets SHA-256 of the guarded context.</summary>
    public string GuardedContextDigest { get; }

    /// <summary>Gets zero for original pixels or the configured modified-context guard.</summary>
    public int AppliedGuard { get; }

    internal static Result<Fs50ContourProcessingRegion, CaptureError> Create(
        Fs50ContactContourShape shape,
        bool modifiedPixels
    )
    {
        var guard = modifiedPixels ? shape.Policy.Options.ModifiedBoundaryGuard : 0;
        var context = shape.ContextMask.ToArray();
        var guarded = Fs50MaskMorphology.InnerAtCuts(context, guard);
        var eligible = new byte[context.Length];
        try
        {
            for (var i = 0; i < eligible.Length; i++)
                eligible[i] = (byte)(shape.FeatureMask[i] != 0 && guarded[i] != 0 ? 1 : 0);
            return FingerprintProcessingMask
                .Create(800, 750, context, eligible)
                .Map(mask => new Fs50ContourProcessingRegion(mask, guarded, guard));
        }
        finally
        {
            Array.Clear(context);
            Array.Clear(guarded);
            Array.Clear(eligible);
        }
    }
}
