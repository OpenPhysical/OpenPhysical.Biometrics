// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class CaptureAdjustmentInput(
    CaptureCoordinator coordinator,
    Maybe<CaptureAcquisitionPolicy> originalPolicy,
    Maybe<CaptureProcessingInput> frozenInput
)
{
    internal Result<CaptureAdjustmentEvidence, DesktopError> For(
        CaptureFrame frame,
        Maybe<CapturedFingerprint> source,
        Maybe<DisplayedFingerprintFrame> displayed,
        CapturePolicySettings current,
        bool newMask
    )
    {
        var live = coordinator.IsLive;
        var input = live ? Maybe.From(coordinator.LiveInput(frame)) : frozenInput;
        Result<Fs50EvidencePolicy, DesktopError> evidence = live
            ? current.Evidence
            : CaptureWorkSnapshot
                .OriginalPolicy(source, originalPolicy)
                .Map(value => value.Settings.Evidence);
        return newMask
            ? evidence.Bind(policy =>
                CaptureAdjustmentEvidence.ForChange(frame, source, policy, input)
            )
            : CaptureAdjustmentEvidence.ForSource(source, displayed, input);
    }

    internal static Maybe<CaptureProcessingInput> Selected(CapturedFingerprint capture) =>
        capture.Processing.Map(evidence =>
            CaptureProcessingInput.ForRevision(
                evidence.InputRole,
                evidence.RuntimeWholeContactProof
            )
        );

    internal static Maybe<Fs50WholeContactProof> ProofFor(
        FingerprintPreprocessingArtifact artifact,
        CaptureProcessingInput input
    )
    {
        var matches = input
            .Proofs.Where(proof =>
                proof.Record.Identity.SourceDigest == artifact.OriginalDigest
                && proof.Record.Identity.Sequence == artifact.OriginalFrame.Sequence
                && proof.Record.Identity.CapturedAt == artifact.OriginalFrame.CapturedAt
            )
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : Maybe<Fs50WholeContactProof>.None;
    }

    internal static Maybe<Fs50ContactRegion> RegionFor(
        CaptureFrame frame,
        Maybe<DisplayedFingerprintFrame> displayed
    ) =>
        displayed.Bind(value =>
            ReferenceEquals(value.Frame, frame) ? value.Segmentation : Maybe<Fs50ContactRegion>.None
        );
}
