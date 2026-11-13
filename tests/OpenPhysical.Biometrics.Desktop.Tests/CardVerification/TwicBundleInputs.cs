// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Middleware.Tests;

internal sealed record TwicBundleInputs(
    ImmutableArray<BundleArtifactDescriptor> Artifacts,
    ImmutableDictionary<string, ImmutableArray<byte>> Contents,
    ImmutableArray<StatusEvidenceDescriptor> Statuses,
    ImmutableArray<string> Profiles
)
{
    internal TwicBundleInputs Add(
        string id,
        BundleArtifactKind kind,
        string profile,
        ImmutableArray<byte> bytes
    ) =>
        this with
        {
            Artifacts = Artifacts.Add(Artifact(id, kind, profile, bytes)),
            Contents = Contents.Add(id, bytes),
        };

    internal static BundleArtifactDescriptor Artifact(
        string id,
        BundleArtifactKind kind,
        string profile,
        ImmutableArray<byte> bytes
    ) =>
        new(
            id,
            kind,
            "application/json",
            bytes.Length,
            VerificationBundleFixture.Hash(bytes),
            profile,
            1
        );
}
