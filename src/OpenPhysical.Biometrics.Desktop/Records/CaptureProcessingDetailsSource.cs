// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record CaptureProcessingDetailsSource(
    CaptureFrame Original,
    Maybe<FingerprintPreprocessingArtifact> Processing,
    Maybe<Fs50ContactDiagnostics> Diagnostics,
    Maybe<CaptureProcessingEvidence> Evidence
);
