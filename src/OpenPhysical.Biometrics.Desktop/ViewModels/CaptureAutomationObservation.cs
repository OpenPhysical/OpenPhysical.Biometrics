// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal sealed record CaptureAutomationObservation(
    bool Present,
    bool ShouldCapture,
    string Guidance
)
{
    internal Maybe<FingerCaptureCandidate> BestFrame { get; init; }
}
