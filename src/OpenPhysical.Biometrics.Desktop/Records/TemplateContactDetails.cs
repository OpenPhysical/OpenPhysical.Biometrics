// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Verified contact-stage metadata and optional diagnostic image planes.</summary>
/// <param name="CandidateMask">Optional diagnostic candidate plane.</param>
/// <param name="RidgeSupportMask">Optional diagnostic direct ridge support plane.</param>
/// <param name="AcceptedMask">Optional diagnostic contact-stage accepted plane.</param>
/// <param name="RejectedMask">Optional diagnostic rejected candidate plane.</param>
/// <param name="InnerMask">Optional diagnostic contact-stage inner plane.</param>
/// <param name="SourceDigest">SHA-256 of the original grayscale pixels.</param>
/// <param name="PolicyDigest">SHA-256 of the contact evidence policy.</param>
/// <param name="Version">The algorithm identifier.</param>
/// <param name="Policy">The validated policy parameters.</param>
/// <param name="Statistics">The verified measured areas and geometry counts.</param>
public sealed record TemplateContactDetails(
    Maybe<ImmutableArray<byte>> CandidateMask,
    Maybe<ImmutableArray<byte>> RidgeSupportMask,
    Maybe<ImmutableArray<byte>> AcceptedMask,
    Maybe<ImmutableArray<byte>> RejectedMask,
    Maybe<ImmutableArray<byte>> InnerMask,
    string SourceDigest,
    string PolicyDigest,
    string Version,
    Fs50EvidencePolicy Policy,
    TemplateContactSummary Statistics
);
