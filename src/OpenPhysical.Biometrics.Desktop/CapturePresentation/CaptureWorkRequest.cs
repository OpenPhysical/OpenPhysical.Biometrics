// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record CaptureWorkRequest(
    ImmutableArray<FingerCaptureCandidate> Candidates,
    CaptureProcessingRequest Processing,
    FingerprintQualitySelectionPolicy Quality,
    CaptureAcquisitionPolicy OriginalAcquisitionPolicy
);
