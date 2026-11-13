// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class CaptureAutomation(TimeProvider clock)
{
    private readonly FingerPresenceGate gate = new(clock);
    private readonly CaptureContactLoss loss = new(clock);
    private bool awaitingRemoval;
    private bool previouslyEnabled;
    private Maybe<FingerCaptureCandidate> selection;
    private ImmutableArray<FingerCaptureCandidate> candidates = [];

    internal bool RequiresRemoval => awaitingRemoval;
    internal bool HasAutomaticSelection => selection.HasValue;

    internal ImmutableArray<FingerCaptureCandidate> TakeCandidates()
    {
        var result = candidates;
        candidates = [];
        if (!result.IsDefaultOrEmpty)
            gate.Reset(awaitingRemoval);
        return result;
    }

    internal UnitResult<DesktopError> ConfigurePolicy(FingerCapturePolicy policy)
    {
        loss.MaximumGap = policy.AcquisitionGap;
        return gate.ConfigurePolicy(policy)
            .MapError(error => new DesktopError(error.Code, error.Message));
    }

    internal Maybe<CaptureAutomationObservation> ObserveContactLoss(
        CaptureFrame frame,
        Maybe<Fs50ContactRegion> contact,
        Maybe<Fs50WholeContactProof> proof,
        bool qualified
    )
    {
        if (contact.HasNoValue || proof.HasNoValue)
        {
            loss.Interrupt();
            return default;
        }
        if (qualified && proof.Value.Qualified && contact.Value.Diagnostics.AcceptedArea > 0)
        {
            if (!awaitingRemoval)
                loss.Remember(frame, contact.Value, proof.Value);
            else
                loss.Interrupt();
            return default;
        }
        var absent = loss.Observe(frame, contact.Value, proof.Value);
        if (absent.HasNoValue)
            return default;
        if (absent.Value)
        {
            awaitingRemoval = false;
            selection = default;
            candidates = [];
            gate.Reset(false);
        }
        return new CaptureAutomationObservation(
            false,
            false,
            absent.Value ? "Place your finger" : CapturePreviewGuidance.Removal
        );
    }

    internal Maybe<FingerCaptureCandidate> TakeSelection()
    {
        var value = selection;
        selection = Maybe<FingerCaptureCandidate>.None;
        return value;
    }

    internal UnitResult<DesktopError> RestartTiming() => Reset(awaitingRemoval);

    internal UnitResult<DesktopError> ConfigureDelay(TimeSpan value)
    {
        var result = gate.ConfigureDelay(value)
            .MapError(error => new DesktopError(error.Code, error.Message));
        return result;
    }

    internal Result<CaptureAutomationObservation, DesktopError> RejectContact()
    {
        var reset = Reset(awaitingRemoval);
        return reset.IsFailure
            ? reset.Error
            : new CaptureAutomationObservation(false, false, "Place your finger");
    }

    internal Result<CaptureAutomationObservation, DesktopError> Observe(
        CaptureFrame frame,
        ImmutableArray<byte> mask,
        int componentCount,
        bool enabled
    )
    {
        if (enabled != previouslyEnabled)
            Reset(awaitingRemoval);
        previouslyEnabled = enabled;
        var result = gate.Observe(frame, mask, componentCount, enabled);
        if (result.IsFailure)
            return new DesktopError(result.Error.Code, result.Error.Message);
        var observed = result.Value;
        awaitingRemoval = observed.RequiresRemoval;
        var trigger = enabled && observed.ShouldCapture;
        selection = trigger ? observed.BestFrame : Maybe<FingerCaptureCandidate>.None;
        candidates = trigger ? gate.Candidates : [];
        var guidance = !observed.Present
            ? awaitingRemoval
                ? CapturePreviewGuidance.Removal
                : "Place your finger"
            : !observed.SingleFinger
                ? "Place one finger"
                : trigger
                    ? "Capturing"
                    : awaitingRemoval
                        ? CapturePreviewGuidance.Removal
                        : !enabled
                            ? "Ready to capture"
                            : $"Keep your finger here · {Math.Ceiling(observed.Remaining.TotalSeconds * 10) / 10:0.#}s";
        if (trigger)
            awaitingRemoval = true;
        return new CaptureAutomationObservation(observed.Present, trigger, guidance)
        {
            BestFrame = selection,
        };
    }

    internal UnitResult<DesktopError> Reset(bool requireRemoval)
    {
        if (!requireRemoval)
            loss.Clear();
        awaitingRemoval = requireRemoval;
        selection = Maybe<FingerCaptureCandidate>.None;
        candidates = [];
        return gate.Reset(requireRemoval)
            .MapError(error => new DesktopError(error.Code, error.Message));
    }
}
