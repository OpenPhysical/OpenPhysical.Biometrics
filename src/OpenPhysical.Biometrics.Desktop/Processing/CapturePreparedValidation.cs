// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal static class CapturePreparedValidation
{
    internal static UnitResult<DesktopError> Check(CapturePreparedFrame prepared)
    {
        var contour = prepared.Contour;
        var artifact = prepared.Artifact;
        var expected = contour.ToProcessingRegion(!artifact.Policy.IsOriginal);
        if (
            expected.IsFailure
            || prepared.Diagnostics.SourceDigest != artifact.OriginalDigest
            || contour.SourceDigest != artifact.OriginalDigest
            || contour.EvidenceVersion != prepared.Diagnostics.Version
            || contour.EvidencePolicyDigest != prepared.Diagnostics.PolicyDigest
            || prepared.ProcessingRegion.AppliedGuard != expected.Value.AppliedGuard
            || prepared.ProcessingRegion.GuardedContextDigest != expected.Value.GuardedContextDigest
            || !expected.Value.Mask.Retained.AsSpan().SequenceEqual(artifact.Mask.Retained.AsSpan())
            || !expected.Value.Mask.Inner.AsSpan().SequenceEqual(artifact.Mask.Inner.AsSpan())
            || !prepared
                .ProcessingRegion.GuardedContextMask.AsSpan()
                .SequenceEqual(expected.Value.GuardedContextMask.AsSpan())
            || !MasksAgree(prepared.ProcessingRegion.Mask, artifact.Mask)
        )
            return Mismatch();
        return CaptureProcessingInput
            .Validate(
                prepared.InputRole,
                prepared.WholeContactProof,
                artifact.OriginalFrame,
                contour
            )
            .Bind(() =>
                CaptureContactFeedback
                    .Validate(prepared.Diagnostics)
                    .Ensure(
                        () => artifact.Mask.Inner.Any(value => value != 0),
                        new DesktopError(
                            "Capture.NoReliableArea",
                            "No reliable finger area remains for fingerprint processing."
                        )
                    )
            );
    }

    private static bool MasksAgree(
        FingerprintProcessingMask first,
        FingerprintProcessingMask second
    ) =>
        ReferenceEquals(first, second)
        || first.Retained.AsSpan().SequenceEqual(second.Retained.AsSpan())
            && first.Inner.AsSpan().SequenceEqual(second.Inner.AsSpan());

    private static UnitResult<DesktopError> Mismatch() =>
        UnitResult.Failure(
            new DesktopError(
                "Capture.ContourSourceMismatch",
                "Contact-contour evidence does not belong to this processing image."
            )
        );
}
