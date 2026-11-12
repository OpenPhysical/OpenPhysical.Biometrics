// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>One immutable image-processing policy snapshot for a frozen source or burst.</summary>
/// <param name="Preprocessing">Fixed pixel controls, never selected by a score search.</param>
/// <param name="Evidence">Independent contact-evidence policy.</param>
/// <param name="Contour">Current context-envelope and artificial-boundary policy.</param>
/// <param name="Revision">Operator revision used for stale-result rejection.</param>
/// <param name="Input">Explicit offline declaration or original frozen-frame physical qualifications.</param>
public sealed record CaptureProcessingRequest(
    FingerprintPreprocessingPolicy Preprocessing,
    Fs50EvidencePolicy Evidence,
    Fs50ContactContourPolicy Contour,
    long Revision,
    CaptureProcessingInput Input
);
