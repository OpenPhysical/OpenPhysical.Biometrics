// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class DisplayedCaptureHistory(TimeProvider clock)
{
    private ImmutableArray<(
        CaptureFrame Frame,
        long At,
        Maybe<Fs50WholeContactProof> Proof
    )> frames = [];
    internal FingerCapturePolicy Policy { get; set; } = FingerCapturePolicy.Default;

    internal UnitResult<DesktopError> ConfigurePolicy(FingerCapturePolicy policy)
    {
        Policy = policy;
        return UnitResult.Success<DesktopError>();
    }

    internal CaptureProcessingInput InputFor(
        CaptureFrame frame,
        Maybe<CaptureFrame> displayed,
        Maybe<Fs50WholeContactProof> displayedProof
    )
    {
        var proof = displayed.Match(value => ReferenceEquals(value, frame), () => false)
            ? displayedProof
            : ProofFor(frame);
        return CaptureProcessingInput.LiveSensor(
            proof.Match(value => ImmutableArray.Create(value), () => [])
        );
    }

    internal bool Add(CaptureFrame frame, Maybe<Fs50WholeContactProof> proof)
    {
        var now = clock.GetTimestamp();
        frames = frames
            .Where(item =>
                !ReferenceEquals(item.Frame, frame)
                && clock.GetElapsedTime(item.At, now) <= Policy.CandidateWindow
            )
            .TakeLast(Policy.CandidateLimit - 1)
            .Append((frame, now, proof))
            .ToImmutableArray();
        return true;
    }

    internal bool Contains(CaptureFrame frame) =>
        frames.Any(item =>
            ReferenceEquals(item.Frame, frame)
            && clock.GetElapsedTime(item.At) <= Policy.CandidateWindow
        );

    internal bool Clear()
    {
        frames = [];
        return true;
    }

    internal Maybe<Fs50WholeContactProof> ProofFor(CaptureFrame frame) =>
        frames
            .Where(item => ReferenceEquals(item.Frame, frame))
            .Select(item => item.Proof)
            .FirstOrDefault();

    internal Result<CaptureSelectionSnapshot, DesktopError> Snapshot(
        CaptureFrame frame,
        ImmutableArray<FingerCaptureCandidate> candidates,
        Maybe<Fs50WholeContactProof> displayedProof
    )
    {
        if (
            candidates.IsDefault
            || candidates.Any(candidate => !Contains(candidate.Frame))
            || (
                !candidates.IsEmpty
                && !candidates.Any(candidate => ReferenceEquals(candidate.Frame, frame))
            )
        )
            return new DesktopError(
                "Capture.UnacknowledgedFrame",
                "The capture selection is not an acknowledged bounded image set."
            );
        var required = candidates.IsEmpty
            ? new[] { frame }
            : candidates.Select(candidate => candidate.Frame).ToArray();
        return CollectProofs(required, frame, displayedProof)
            .Map(proofs => new CaptureSelectionSnapshot(
                frame,
                candidates,
                CaptureProcessingInput.LiveSensor(proofs)
            ));
    }

    private Result<ImmutableArray<Fs50WholeContactProof>, DesktopError> CollectProofs(
        CaptureFrame[] required,
        CaptureFrame frame,
        Maybe<Fs50WholeContactProof> displayedProof
    )
    {
        var proofs = ImmutableArray.CreateBuilder<Fs50WholeContactProof>();
        foreach (var item in required)
        {
            var proof = ReferenceEquals(item, frame)
                ? displayedProof.Or(ProofFor(item))
                : ProofFor(item);
            if (proof.HasNoValue || !proof.Value.Qualified)
                return new DesktopError(
                    "Capture.WholeContactUnqualified",
                    "Wait for qualified contact before capturing."
                );
            if (!proofs.Any(existing => ReferenceEquals(existing, proof.Value)))
                proofs.Add(proof.Value);
        }
        return proofs.ToImmutable();
    }
}
