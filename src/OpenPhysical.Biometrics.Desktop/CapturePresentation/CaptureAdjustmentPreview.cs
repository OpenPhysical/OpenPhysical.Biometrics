// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class CaptureAdjustmentPreview
{
    internal static DisplayedFingerprintFrame Create(
        FingerprintPreprocessingArtifact artifact,
        CaptureAdjustmentEvidence evidence,
        Maybe<DisplayedFingerprintFrame> previous
    )
    {
        var region = evidence.Contact.Or(
            CaptureAdjustmentInput.RegionFor(artifact.OriginalFrame, previous)
        );
        var proof = CaptureAdjustmentInput.ProofFor(artifact, evidence.Input);
        return new(artifact.OriginalFrame, region)
        {
            Processing = artifact,
            Ready = proof.Match(value => value.Qualified, () => false),
            WholeContactProof = proof,
        };
    }
}
