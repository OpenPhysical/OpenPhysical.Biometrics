// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Linq;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Domain.Trust;
using AcquisitionPolicy = OpenPhysical.Middleware.Infrastructure.Trust.GuidedFingerprintCaptureService.AcquisitionPolicy;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Reads only the authenticated acquisition policy for the requested credential and device.</summary>
internal static class FingerprintAcquisitionPolicyReader
{
    internal static Result<AcquisitionPolicy, VerificationError> Read(
        CredentialVerificationRequest request,
        string deviceId
    )
    {
        var descriptors = request
            .Bundle.Manifest.Artifacts.Where(value =>
                value.Kind == BundleArtifactKind.VerificationProfile
                && value.ProfileId == "openphysical.fingerprint-acquisition-policy"
                && value.ProfileVersion == 1
                && value.MediaType == "application/json"
            )
            .ToArray();
        if (descriptors.Length != 1)
            return Result.Failure<AcquisitionPolicy, VerificationError>(
                new(VerificationErrorCodes.EvidenceUnavailable, "capture.signed-policy-missing")
            );
        return VerificationManifestCodec
            .Decode(
                request.Bundle.AuthenticatedArtifacts[descriptors[0].ArtifactId],
                root => ReadContext(root, request.ProfileId, deviceId)
            )
            .Bind(result => result);
    }

    private static Result<AcquisitionPolicy, VerificationError> ReadContext(
        JsonElement root,
        string profileId,
        string deviceId
    )
    {
        if (
            root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("credentialProfileId").GetString() != profileId
        )
            return Invalid();
        var profiles = root.GetProperty("devices");
        if (profiles.GetArrayLength() is < 1 or > 64)
            return Invalid();
        var matches = profiles
            .EnumerateArray()
            .Where(value => value.GetProperty("deviceId").GetString() == deviceId)
            .ToArray();
        return matches.Length == 1 ? ReadDevice(matches[0], deviceId) : Invalid();
    }

    private static Result<AcquisitionPolicy, VerificationError> ReadDevice(
        JsonElement value,
        string deviceId
    )
    {
        var geometry = CaptureGeometry.Create(
            value.GetProperty("width").GetInt32(),
            value.GetProperty("height").GetInt32()
        );
        if (geometry.IsFailure)
            return Invalid();
        var profile = FingerprintAcquisitionProfile.Create(
            value.GetProperty("profileId").GetString()!,
            deviceId,
            value.GetProperty("driverVersion").GetString()!,
            value.GetProperty("correctionVersion").GetString()!,
            Convert.FromHexString(value.GetProperty("calibrationSha256").GetString()!),
            value.GetProperty("qualificationId").GetString()!,
            value.GetProperty("equipmentId").GetUInt16(),
            value.GetProperty("appendixFCertified").GetBoolean(),
            geometry.Value
        );
        var seconds = value.GetProperty("lifetimeSeconds").GetInt32();
        if (profile.IsFailure || seconds is < 1 or > 120)
            return Invalid();
        var settings = CaptureSettings.Create(
            value.GetProperty("exposure").GetInt32(),
            false,
            true,
            TimeSpan.FromSeconds(seconds)
        );
        return settings.IsSuccess
            ? new AcquisitionPolicy(profile.Value, settings.Value)
            : Invalid();
    }

    private static Result<AcquisitionPolicy, VerificationError> Invalid() =>
        Result.Failure<AcquisitionPolicy, VerificationError>(
            new(VerificationErrorCodes.InvalidInput, "bundle.decode")
        );
}
