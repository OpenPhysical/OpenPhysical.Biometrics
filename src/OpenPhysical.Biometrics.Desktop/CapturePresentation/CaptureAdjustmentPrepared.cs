// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture;

internal sealed record CaptureAdjustmentPrepared(
    FingerprintPreprocessingArtifact Artifact,
    CaptureAdjustmentEvidence Evidence
);
