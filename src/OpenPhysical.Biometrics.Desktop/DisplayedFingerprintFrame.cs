// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Associates the displayed acquisition with its exact processing and readiness evidence.</summary>
/// <param name="Frame">Immutable acquisition shown to the operator.</param>
/// <param name="Segmentation">Same-frame contact evidence, when available.</param>
public sealed record DisplayedFingerprintFrame(
    CaptureFrame Frame,
    Maybe<Fs50ContactRegion> Segmentation
)
{
    /// <summary>Gets the exact display processing artifact, when available.</summary>
    public Maybe<FingerprintPreprocessingArtifact> Processing { get; init; }

    /// <summary>Gets whether session reference learning permits capture.</summary>
    public bool Ready { get; init; } = true;

    /// <summary>Gets the original exact-frame qualification, never inferred from a later readiness flag.</summary>
    public Maybe<Fs50WholeContactProof> WholeContactProof { get; init; }
}
