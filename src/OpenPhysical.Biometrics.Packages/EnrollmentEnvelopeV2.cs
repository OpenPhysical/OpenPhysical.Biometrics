// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Distinct contexts in which an enrollment envelope may be transferred.</summary>
public enum EnrollmentTransferPurpose
{
    /// <summary>Local workstation draft before upload.</summary>
    LocalDraft = 1,

    /// <summary>Transfer from a capture workstation to a service.</summary>
    Upload = 2,

    /// <summary>Transfer to an authorized delivery recipient.</summary>
    Delivery = 3,

    /// <summary>Encrypted retained storage.</summary>
    Storage = 4,
}

/// <summary>Origin fields that remain stable when encrypted content is re-encrypted.</summary>
public sealed record EnrollmentOrigin
{
    private EnrollmentOrigin(
        string organizationId,
        string personId,
        string enrollmentId,
        uint revision,
        string purpose,
        string stationId,
        ImmutableArray<byte> capturePolicyEvidence,
        ImmutableArray<byte> manifestDer,
        ImmutableArray<byte> signature,
        ImmutableArray<byte> signingKeyId
    )
    {
        OrganizationId = organizationId;
        PersonId = personId;
        EnrollmentId = enrollmentId;
        Revision = revision;
        Purpose = purpose;
        StationId = stationId;
        CapturePolicyEvidence = capturePolicyEvidence;
        ManifestDer = manifestDer;
        Signature = signature;
        SigningKeyId = signingKeyId;
    }

    /// <summary>Gets the origin organization identifier.</summary>
    public string OrganizationId { get; }

    /// <summary>Gets the opaque person identifier.</summary>
    public string PersonId { get; }

    /// <summary>Gets the stable enrollment identifier.</summary>
    public string EnrollmentId { get; }

    /// <summary>Gets the immutable origin revision.</summary>
    public uint Revision { get; }

    /// <summary>Gets the origin capture purpose.</summary>
    public string Purpose { get; }

    /// <summary>Gets the capture station identifier.</summary>
    public string StationId { get; }

    /// <summary>Gets exact capture policy evidence bytes.</summary>
    public ImmutableArray<byte> CapturePolicyEvidence { get; }

    /// <summary>Gets canonical unsigned origin statement DER.</summary>
    public ImmutableArray<byte> ManifestDer { get; }

    /// <summary>Gets the origin statement ECDSA signature.</summary>
    public ImmutableArray<byte> Signature { get; }

    /// <summary>Gets the origin signing key fingerprint.</summary>
    public ImmutableArray<byte> SigningKeyId { get; }

    /// <summary>Gets a domain-separated stable idempotency digest.</summary>
    public string IdempotencyDigest => EnrollmentOriginArtifactCodec.IdempotencyDigest(this);

    /// <summary>Builds and signs the canonical origin manifest over its exact DER bytes.</summary>
    /// <param name="organizationId">Origin organization identifier.</param>
    /// <param name="personId">Opaque person identifier.</param>
    /// <param name="enrollmentId">Stable enrollment identifier.</param>
    /// <param name="revision">Immutable origin revision.</param>
    /// <param name="purpose">Capture purpose recorded by the station.</param>
    /// <param name="stationId">Origin station identifier.</param>
    /// <param name="capturePolicyEvidence">Exact encoded capture policy evidence.</param>
    /// <param name="artifacts">Ordered artifacts described by the statement.</param>
    /// <param name="signer">Opaque origin signing operation.</param>
    /// <returns>A signed origin statement or a typed failure.</returns>
    public static Result<EnrollmentOrigin, PackageError> Create(
        string organizationId,
        string personId,
        string enrollmentId,
        uint revision,
        string purpose,
        string stationId,
        ReadOnlySpan<byte> capturePolicyEvidence,
        ImmutableArray<PackageArtifact> artifacts,
        IEnrollmentSigner signer
    ) =>
        EnrollmentOriginCodec.Create(
            organizationId,
            personId,
            enrollmentId,
            revision,
            purpose,
            stationId,
            capturePolicyEvidence,
            artifacts,
            signer
        );

    internal static EnrollmentOrigin FromParsed(
        string organizationId,
        string personId,
        string enrollmentId,
        uint revision,
        string purpose,
        string stationId,
        byte[] evidence,
        byte[] manifest,
        byte[] signature,
        byte[] keyId
    ) =>
        new(
            organizationId,
            personId,
            enrollmentId,
            revision,
            purpose,
            stationId,
            ImmutableArray.Create(evidence),
            ImmutableArray.Create(manifest),
            ImmutableArray.Create(signature),
            ImmutableArray.Create(keyId)
        );
}

/// <summary>One transfer request with immutable origin provenance.</summary>
public sealed record EnrollmentEnvelopeTransfer
{
    /// <summary>Creates the transfer metadata and ordered artifact collection.</summary>
    /// <param name="organizationId">Transfer organization identifier.</param>
    /// <param name="transferId">Unique opaque transfer identifier.</param>
    /// <param name="senderId">Opaque sender station or service identifier.</param>
    /// <param name="recipientId">Opaque receiving station or service identifier.</param>
    /// <param name="enrollmentId">Stable enrollment identifier.</param>
    /// <param name="revision">Origin revision carried by this transfer.</param>
    /// <param name="purpose">Transfer context.</param>
    /// <param name="createdUtc">UTC creation time.</param>
    /// <param name="expiresUtc">UTC expiry time.</param>
    /// <param name="authorizationReferences">Opaque authorization record references.</param>
    /// <param name="origin">Signed immutable capture origin.</param>
    /// <param name="artifacts">Ordered exact artifact contents.</param>
    public EnrollmentEnvelopeTransfer(
        string organizationId,
        string transferId,
        string senderId,
        string recipientId,
        string enrollmentId,
        uint revision,
        EnrollmentTransferPurpose purpose,
        DateTimeOffset createdUtc,
        DateTimeOffset expiresUtc,
        ImmutableArray<string> authorizationReferences,
        EnrollmentOrigin origin,
        ImmutableArray<PackageArtifact> artifacts
    )
    {
        OrganizationId = organizationId;
        TransferId = transferId;
        SenderId = senderId;
        RecipientId = recipientId;
        EnrollmentId = enrollmentId;
        Revision = revision;
        Purpose = purpose;
        CreatedUtc = createdUtc;
        ExpiresUtc = expiresUtc;
        AuthorizationReferences = authorizationReferences;
        Origin = origin;
        Artifacts = artifacts;
    }

    /// <summary>Gets the transfer organization identifier.</summary>
    public string OrganizationId { get; }

    /// <summary>Gets the unique opaque transfer identifier.</summary>
    public string TransferId { get; }

    /// <summary>Gets the opaque sending station or service identifier.</summary>
    public string SenderId { get; }

    /// <summary>Gets the opaque receiving station or service identifier.</summary>
    public string RecipientId { get; }

    /// <summary>Gets the stable enrollment identifier.</summary>
    public string EnrollmentId { get; }

    /// <summary>Gets the origin revision.</summary>
    public uint Revision { get; }

    /// <summary>Gets the transfer context.</summary>
    public EnrollmentTransferPurpose Purpose { get; }

    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>Gets the UTC expiry time.</summary>
    public DateTimeOffset ExpiresUtc { get; }

    /// <summary>Gets opaque authorization record references.</summary>
    public ImmutableArray<string> AuthorizationReferences { get; }

    /// <summary>Gets immutable signed capture origin.</summary>
    public EnrollmentOrigin Origin { get; }

    /// <summary>Gets artifacts in signed origin order.</summary>
    public ImmutableArray<PackageArtifact> Artifacts { get; }
}
