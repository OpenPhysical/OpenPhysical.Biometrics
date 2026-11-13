// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Linq;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using MatchingProfile = OpenPhysical.Middleware.Infrastructure.Trust.FingerprintReferenceAuthenticator.MatchingProfile;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Reads the one authenticated matching policy authorized for this credential profile.</summary>
internal static class FingerprintMatchingPolicyReader
{
    internal static Result<MatchingProfile, VerificationError> Read(
        CredentialVerificationRequest request
    )
    {
        var descriptors = request
            .Bundle.Manifest.Artifacts.Where(value =>
                value.Kind == BundleArtifactKind.VerificationProfile
                && value.ProfileId == "openphysical.fingerprint-matching-policy"
                && value.ProfileVersion == 1
                && value.MediaType == "application/json"
            )
            .ToArray();
        if (
            descriptors.Length != 1
            || !request.Bundle.Policy.AllowedProfileIds.Contains(request.ProfileId)
        )
            return Result.Failure<MatchingProfile, VerificationError>(
                new(VerificationErrorCodes.EvidenceUnavailable, "ceremony.fingerprint-reference")
            );
        return VerificationManifestCodec
            .Decode(
                request.Bundle.AuthenticatedArtifacts[descriptors[0].ArtifactId],
                root => ReadFields(root, request.ProfileId)
            )
            .Bind(result => result);
    }

    private static Result<MatchingProfile, VerificationError> ReadFields(
        JsonElement root,
        string profileId
    )
    {
        if (
            root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("profileId").GetString() != profileId
        )
            return Invalid();
        var identifier = root.GetProperty("matchingPolicyId");
        if (identifier.ValueKind != JsonValueKind.String)
            return Invalid();
        string text = identifier.GetString()!;
        int score = root.GetProperty("minimumScore").GetInt32();
        int age = root.GetProperty("maximumCaptureAgeSeconds").GetInt32();
        return text.Length is >= 1 and <= 256 && score > 0 && age is >= 1 and <= 120
            ? new MatchingProfile(text, score, TimeSpan.FromSeconds(age))
            : Invalid();
    }

    private static Result<MatchingProfile, VerificationError> Invalid() =>
        Result.Failure<MatchingProfile, VerificationError>(
            new(VerificationErrorCodes.InvalidInput, "bundle.decode")
        );
}
