// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using CSharpFunctionalExtensions;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Inspection;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Reads exact encrypted TWIC reference objects only under their authenticated generation and profile.</summary>
internal static class TwicFingerprintReferenceReader
{
    internal static Result<ReferenceData, VerificationError> Read(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        ILocalCardSession session,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
            return Failed(VerificationErrorCodes.Cancelled);
        if (
            session.Card.Mode != ConnectionMode.Exclusive
            || session.EnsureCurrent().IsFailure
            || request.Workflow != CredentialVerificationWorkflow.Twic
            || request.ProfileId != profile.ProfileId
            || !TwicAcceptanceService.AuthenticatedProfile(request.Bundle, profile)
        )
            return Failed();
        var decoded = TwicCredentialEvidenceCodec.Decode(request);
        if (
            decoded.IsFailure
            || decoded.Value.Generation != profile.Generation
            || TwicMiddleware.TwicSelectApplication(
                Maybe<ICard>.From(session.Card),
                out var selection
            ) != PivStatusWord.PivOk
            || selection.Model.HasNoValue
            || selection.Model.Value != profile.Generation
        )
            return Failed();
        return ReadObjects(decoded.Value, profile.Generation, session, token);
    }

    private static Result<ReferenceData, VerificationError> ReadObjects(
        TwicEnvelope evidence,
        TwicCardModel generation,
        ILocalCardSession session,
        CancellationToken token
    )
    {
        var definitions = TwicDataObjectRegistry.GetDefinitions(generation);
        var chuidDefinition = definitions.Single(value =>
            value.Name == "Cardholder Unique Identifier (CHUID)"
        );
        var fingerprintDefinition = definitions.Single(value =>
            value.Name == "Cardholder Fingerprints (TPK Encrypted)"
        );
        return ReadSelectedObjects(
            evidence,
            session,
            chuidDefinition,
            fingerprintDefinition,
            token
        );
    }

    private static Result<ReferenceData, VerificationError> ReadSelectedObjects(
        TwicEnvelope evidence,
        ILocalCardSession session,
        TwicDataObjectDefinition chuidDefinition,
        TwicDataObjectDefinition fingerprintDefinition,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
            return Failed(VerificationErrorCodes.Cancelled);
        if (
            TwicMiddleware.TwicGetData(
                Maybe<ICard>.From(session.Card),
                chuidDefinition,
                65535,
                out var chuid
            ) != PivStatusWord.PivOk
            || !chuid.AsSpan().SequenceEqual(evidence.SignedChuid.AsSpan())
        )
            return Failed();
        if (token.IsCancellationRequested)
            return Failed(VerificationErrorCodes.Cancelled);
        if (
            session.EnsureCurrent().IsFailure
            || TwicMiddleware.TwicGetData(
                Maybe<ICard>.From(session.Card),
                fingerprintDefinition,
                65535,
                out var encrypted
            ) != PivStatusWord.PivOk
            || !encrypted.AsSpan().SequenceEqual(evidence.EncryptedFingerprint.AsSpan())
        )
            return Failed();
        return new ReferenceData(chuid, encrypted, evidence.ContentSignerChain);
    }

    private static Result<ReferenceData, VerificationError> Failed(
        VerificationErrorCodes code = VerificationErrorCodes.EvidenceUnavailable
    ) =>
        Result.Failure<ReferenceData, VerificationError>(
            new(code, "ceremony.reference-unavailable")
        );

    internal sealed record ReferenceData(
        ImmutableArray<byte> Chuid,
        ImmutableArray<byte> Encrypted,
        ImmutableArray<X509Certificate> ContentSignerChain
    );
}
