// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text.Json;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Middleware.Tests;

internal static class FingerprintBundleFixture
{
    internal static async Task<VerifiedBundleSnapshot> CreateAsync(
        string profile,
        string device,
        string scenario
    )
    {
        var bytes = MatchingPolicy(profile, "fixture-match-v1");
        var artifacts = ImmutableArray.Create(
            Artifact("matching", "openphysical.fingerprint-matching-policy", bytes)
        );
        var content = ImmutableDictionary<string, ImmutableArray<byte>>.Empty.Add(
            "matching",
            bytes
        );
        if (
            scenario.StartsWith("acquisition", StringComparison.Ordinal)
            && scenario != "acquisition-policy-missing"
        )
        {
            var acquisition = AcquisitionPolicy(profile, device);
            artifacts = artifacts.Add(
                Artifact("acquisition", "openphysical.fingerprint-acquisition-policy", acquisition)
            );
            content = content.Add("acquisition", acquisition);
        }
        return (
            await new VerificationBundleFixture(
                extra: artifacts,
                content: content,
                policyOverride: VerificationPolicy(profile)
            ).Verify()
        ).Value;
    }

    private static VerificationPolicySnapshot VerificationPolicy(string profile) =>
        new(
            "test-policy",
            1,
            TimeSpan.FromHours(24),
            TimeSpan.FromSeconds(60),
            true,
            false,
            false,
            [profile]
        );

    internal static BundleArtifactDescriptor Artifact(
        string id,
        string profile,
        ImmutableArray<byte> bytes
    ) =>
        new(
            id,
            BundleArtifactKind.VerificationProfile,
            "application/json",
            bytes.Length,
            VerificationBundleFixture.Hash(bytes),
            profile,
            1
        );

    internal static ImmutableArray<byte> MatchingPolicy(
        string profileId,
        string matchingPolicyId
    ) =>
        JsonSerializer
            .SerializeToUtf8Bytes(
                new
                {
                    schemaVersion = 1,
                    profileId,
                    matchingPolicyId,
                    minimumScore = 50,
                    maximumCaptureAgeSeconds = 60,
                }
            )
            .ToImmutableArray();

    internal static ImmutableArray<byte> AcquisitionPolicy(string profileId, string deviceId) =>
        JsonSerializer
            .SerializeToUtf8Bytes(
                new
                {
                    schemaVersion = 1,
                    credentialProfileId = profileId,
                    devices = new[]
                    {
                        new
                        {
                            deviceId,
                            profileId = "fixture",
                            driverVersion = "1",
                            correctionVersion = "1",
                            calibrationSha256 = new string('0', 64),
                            qualificationId = "fixture-only",
                            equipmentId = 3,
                            appendixFCertified = false,
                            width = 128,
                            height = 128,
                            lifetimeSeconds = 30,
                            exposure = 200,
                        },
                    },
                }
            )
            .ToImmutableArray();
}
