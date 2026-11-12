// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Capture;

internal sealed record CaptureSelectionSnapshot(
    CaptureFrame Frame,
    ImmutableArray<FingerCaptureCandidate> Candidates,
    CaptureProcessingInput Input
);
