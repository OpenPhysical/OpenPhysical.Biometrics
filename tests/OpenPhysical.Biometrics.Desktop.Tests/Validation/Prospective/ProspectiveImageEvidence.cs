// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;

internal sealed record ProspectiveImageEvidence(
    string OriginalDigest,
    string AssessedImageDigest,
    string RegionPolicyDigest,
    int Guard,
    int FeatureMaskArea,
    Maybe<int> ExtractedFeatures,
    Maybe<int> ExportedFeatures,
    EvaluationQualitySnapshot[] Quality
);
