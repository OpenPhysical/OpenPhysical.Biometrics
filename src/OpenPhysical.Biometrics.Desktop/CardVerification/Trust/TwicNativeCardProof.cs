// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.Middleware.Diagnostics;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.CredentialSnapshots;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Twic.Codec;
using ApplicationId = OpenPhysical.CardEdge.Application.ApplicationId;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

internal static class TwicNativeCardProof
{
    internal static Result<CredentialVerificationRequest, VerificationError> Execute(
        TwicLiveSession live,
        CredentialVerificationRequest value,
        CancellationToken cancellationToken
    )
    {
        if (!live.IsCurrent)
            return ProofFailure();
        var selected = ReadAuthenticationCertificate(live, cancellationToken);
        if (selected.IsFailure)
            return Result.Failure<CredentialVerificationRequest, VerificationError>(selected.Error);
        var certificate = selected.Value;
        var proof = CredentialCardProof.Execute(
            live.Authority,
            live.Executor,
            value,
            live.Session.Card,
            certificate.GetEncoded().ToImmutableArray(),
            KeySlot.CardAuthentication,
            cancellationToken
        );
        if (proof.IsFailure)
            return Result.Failure<CredentialVerificationRequest, VerificationError>(proof.Error);
        return live.IsCurrent
            ? live.Copy(value, proof.Value, value.HolderEvidence)
            : ProofFailure();
    }

    private static Result<
        Org.BouncyCastle.X509.X509Certificate,
        VerificationError
    > ReadAuthenticationCertificate(TwicLiveSession live, CancellationToken cancellationToken)
    {
        var card = Maybe<ICard>.From(live.Session.Card);
        if (!Select(live, card))
            return CertificateFailure();
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = PivCredentialSnapshotAcquirer.Acquire(
            card,
            live.Session.InterfaceType,
            shouldReadObject: definition =>
                definition.Tag.Value == DataObjectTag.CardAuthenticationCertificate.Value,
            stopOnCardReaderError: true
        );
        if (snapshot.IsFailure)
            return CertificateFailure();
        var certificate = PivSnapshotCertificateReader
            .EnumerateCertificateObjects(snapshot.Value, false, false)
            .SingleOrDefault(item =>
                item.Tag.Value == DataObjectTag.CardAuthenticationCertificate.Value
            );
        if (
            certificate is null
            || !live.IsCurrent
            || !certificate
                .Certificate.GetEncoded()
                .AsSpan()
                .SequenceEqual(live.Envelope.AuthenticationChain[0].GetEncoded())
        )
            return CertificateFailure();
        return certificate.Certificate;
    }

    private static bool Select(TwicLiveSession live, Maybe<ICard> card)
    {
        if (live.Profile.Generation == TwicCardModel.Legacy)
        {
            if (
                PivMiddleware.PivSelectApplication(card, ApplicationId.PivApplication, 2048, out _)
                != PivStatusWord.PivOk
            )
                return false;
        }
        else if (
            TwicMiddleware.TwicSelectApplication(card, out var selected) != PivStatusWord.PivOk
            || selected.Model.HasNoValue
            || selected.Model.Value != live.Profile.Generation
        )
            return false;
        return true;
    }

    private static Result<
        Org.BouncyCastle.X509.X509Certificate,
        VerificationError
    > CertificateFailure() =>
        Result.Failure<Org.BouncyCastle.X509.X509Certificate, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "twic.card-proof-unavailable")
        );

    private static Result<CredentialVerificationRequest, VerificationError> ProofFailure() =>
        Result.Failure<CredentialVerificationRequest, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "twic.card-proof-unavailable")
        );
}
