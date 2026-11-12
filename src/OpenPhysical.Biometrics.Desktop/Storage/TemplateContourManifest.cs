// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateContourManifest(
    int Version,
    TemplateContourPlaneFiles Planes,
    string SourceDigest,
    string EvidencePolicyDigest,
    string EvidenceVersion,
    TemplateContourPolicyMetadata Policy,
    string PolicyDigest,
    string AlgorithmVersion,
    Fs50ContactContourStatistics Statistics,
    int AppliedGuard,
    string ProcessingIdentity,
    Fs50WholeContactRidgeEvidence RidgeEvidence
);
