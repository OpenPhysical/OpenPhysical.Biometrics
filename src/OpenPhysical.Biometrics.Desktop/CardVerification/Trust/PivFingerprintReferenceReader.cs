// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using CSharpFunctionalExtensions;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.SmartCard;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Inspection;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Matches signed PIV reference bytes to the original exclusive card before holder guidance.</summary>
internal static class PivFingerprintReferenceReader
{
    internal static Result<ReferenceData, VerificationError> Read(
        CredentialVerificationRequest request,
        ILocalCardSession session,
        CancellationToken token
    )
    {
        if (
            request.Workflow == CredentialVerificationWorkflow.Twic
            || session.Card.Mode != ConnectionMode.Exclusive
            || session.EnsureCurrent().IsFailure
            || request.ProfileId != CredentialEvidenceCodec.CardAuthenticationProfile
                && request.ProfileId != CredentialEvidenceCodec.PinAuthenticationProfile
        )
            return Failed();
        var snapshot = PivCardInspectionService.Read(session, token);
        if (snapshot.IsFailure)
            return snapshot.Error;
        return Match(request, snapshot.Value);
    }

    private static Result<ReferenceData, VerificationError> Match(
        CredentialVerificationRequest request,
        PivCredentialSnapshot snapshot
    )
    {
        var tag =
            request.ProfileId == CredentialEvidenceCodec.PinAuthenticationProfile
                ? DataObjectTag.PivAuthenticationCertificate
                : DataObjectTag.CardAuthenticationCertificate;
        var path = CredentialEvidenceCodec.Decode(request);
        var certificates = PivSnapshotCertificateReader
            .EnumerateCertificateObjects(snapshot, false, false)
            .Where(value => value.Tag.Value == tag.Value)
            .ToArray();
        if (
            path.IsFailure
            || path.Value.IsEmpty
            || certificates.Length != 1
            || !certificates[0]
                .Certificate.GetEncoded()
                .AsSpan()
                .SequenceEqual(path.Value[0].GetEncoded())
        )
            return Failed();
        var chuid = snapshot
            .DataObjects.Where(value =>
                value.Tag.Value == DataObjectTag.CardholderUniqueIdentifier.Value
                && value.Status == CardAccessStatus.Ok
            )
            .ToArray();
        var fingerprint = snapshot
            .DataObjects.Where(value =>
                value.Tag.Value == DataObjectTag.CardholderFingerprints.Value
                && value.Status == CardAccessStatus.Ok
            )
            .ToArray();
        return chuid.Length == 1 && fingerprint.Length == 1
            ? new ReferenceData(chuid[0].Payload, fingerprint[0].Payload)
            : Failed();
    }

    private static Result<ReferenceData, VerificationError> Failed() =>
        Result.Failure<ReferenceData, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "ceremony.reference-unavailable")
        );

    internal sealed record ReferenceData(
        ImmutableArray<byte> Chuid,
        ImmutableArray<byte> Fingerprint
    );
}
