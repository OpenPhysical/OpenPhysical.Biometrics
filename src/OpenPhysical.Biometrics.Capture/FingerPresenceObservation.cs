// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using CSharpFunctionalExtensions;

/// <summary>Presence and a one-shot automatic capture decision for one exact frame.</summary>
/// <param name="Present">Whether the supplied foreground mask contains contact.</param>
/// <param name="SingleFinger">Whether exactly one foreground component is present.</param>
/// <param name="ShouldCapture">Triggers once after the configured stable-presence interval.</param>
public sealed record FingerPresenceObservation(bool Present, bool SingleFinger, bool ShouldCapture)
{
    /// <summary>Gets the remaining stable-presence interval for this exact observation.</summary>
    public TimeSpan Remaining { get; init; }

    /// <summary>Gets the best eligible acknowledged image when capture is triggered.</summary>
    public Maybe<FingerCaptureCandidate> BestFrame { get; init; }

    /// <summary>Gets whether source, sustained major motion, absence or a frame gap cleared the old presentation.</summary>
    public bool PresentationCleared { get; init; }

    /// <summary>Gets whether a consumed presentation must be removed before automatic capture can rearm.</summary>
    public bool RequiresRemoval { get; init; }
}
