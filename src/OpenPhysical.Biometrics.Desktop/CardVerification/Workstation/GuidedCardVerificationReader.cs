// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Middleware.Twic.Codec;
using OpenPhysical.Workstation.Application.Devices;
using OpenPhysical.Workstation.Application.Verification;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Workstation.Infrastructure.Verification;

internal sealed class GuidedCardVerificationReader(
    DeviceSessionCoordinator devices,
    LiveFingerprintVerificationService piv,
    LiveTwicFingerprintVerificationService twic,
    WorkstationClockConfidence confidence,
    TimeProvider clock
)
{
    internal async Task<Result<CredentialVerificationResult, VerificationError>> RunAsync(
        GuidedVerificationInput input
    )
    {
        input.Token.ThrowIfCancellationRequested();
        var selected = SelectProfile(input);
        if (selected.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(selected.Error);
        var profile = selected.Value;
        var application =
            profile.HasNoValue ? LocalCardApplication.Piv
            : profile.Value.Generation == TwicCardModel.Legacy ? LocalCardApplication.TwicLegacy
            : LocalCardApplication.TwicNexgen;
        var opened = devices.Open(input.Request.ReaderId, input.Request.InterfaceType, application);
        if (opened.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(opened.Error);
        using var session = opened.Value;
        if (input.Request.Kind == GuidedCredentialKind.Piv)
            return await ReadPivAsync(session, input).ConfigureAwait(false);
        if (profile.HasNoValue)
            return Failed();
        return await ReadTwicAsync(session, input, profile.Value).ConfigureAwait(false);
    }

    private static Result<Maybe<TwicAcceptanceProfile>, VerificationError> SelectProfile(
        GuidedVerificationInput input
    )
    {
        if (input.Request.Kind != GuidedCredentialKind.Twic)
            return Result.Success<Maybe<TwicAcceptanceProfile>, VerificationError>(
                Maybe<TwicAcceptanceProfile>.None
            );
        var selected = TwicAcceptanceService.ReadProfile(input.Bundle, input.Request.ProfileId);
        return selected.IsFailure
            ? Result.Failure<Maybe<TwicAcceptanceProfile>, VerificationError>(selected.Error)
            : Maybe.From(selected.Value);
    }

    private async Task<Result<CredentialVerificationResult, VerificationError>> ReadPivAsync(
        ILocalCardSession session,
        GuidedVerificationInput input
    )
    {
        if (input.Request.ProfileId != CredentialEvidenceCodec.CardAuthenticationProfile)
            return Failed();
        var prepared = WorkstationVerificationEvidence.ReadPiv(
            session,
            LocalRequest(input),
            input.Bundle,
            confidence.Uncertainty,
            clock.GetUtcNow(),
            input.Token
        );
        if (prepared.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(prepared.Error);
        var signers = ContentPath(
            prepared.Value.Chuid,
            input.Request.ContentSignerCertificates,
            input.Bundle
        );
        if (signers.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(signers.Error);
        return await piv.VerifyAsync(
                session,
                prepared.Value.Request,
                signers.Value,
                input.Request.CaptureDeviceId,
                input.Pin,
                input.GuideAsync,
                input.Token
            )
            .ConfigureAwait(false);
    }

    private static LocalVerificationRequest LocalRequest(GuidedVerificationInput input) =>
        new(
            input.Request.ReaderId,
            input.Request.InterfaceType,
            LocalCardVerificationMethod.CardAuthentication,
            input.Request.IssuerCertificates
        );

    private async Task<Result<CredentialVerificationResult, VerificationError>> ReadTwicAsync(
        ILocalCardSession session,
        GuidedVerificationInput input,
        TwicAcceptanceProfile profile
    )
    {
        var evaluation = ReadTwicEvidence(session, input, profile);
        if (evaluation.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(
                evaluation.Error
            );
        var parsed = GuidedTwicPrivacyKey.Parse(input.PrivacyKey);
        if (parsed.IsFailure)
            return Failed();
        using var key = parsed.Value;
        return await twic.VerifyAsync(
                session,
                evaluation.Value,
                profile,
                input.Request.MarsecLevel,
                key.Value,
                input.Request.CaptureDeviceId,
                input.GuideAsync,
                input.Token
            )
            .ConfigureAwait(false);
    }

    private Result<CredentialVerificationRequest, VerificationError> ReadTwicEvidence(
        ILocalCardSession session,
        GuidedVerificationInput input,
        TwicAcceptanceProfile profile
    )
    {
        var acquired = TwicPublicEvidenceAcquirer.Read(session, profile.Generation, input.Token);
        if (acquired.IsFailure)
            return Result.Failure<CredentialVerificationRequest, VerificationError>(acquired.Error);
        var content = ContentPath(
            acquired.Value.SignedChuid,
            input.Request.ContentSignerCertificates,
            input.Bundle
        );
        var certificate = new X509CertificateParser().ReadCertificate(
            acquired.Value.AuthenticationCertificate.ToArray()
        );
        var authentication = CredentialCertificatePathBuilder.Build(
            certificate,
            input.Request.IssuerCertificates,
            input.Bundle,
            clock.GetUtcNow()
        );
        if (content.IsFailure || authentication.IsFailure)
            return WorkstationVerificationEvidence.Failure<CredentialVerificationRequest>();
        return EncodeTwic(input, acquired.Value, authentication.Value, content.Value);
    }

    private CredentialVerificationRequest EncodeTwic(
        GuidedVerificationInput input,
        TwicPublicEvidence acquired,
        ImmutableArray<ImmutableArray<byte>> authentication,
        ImmutableArray<X509Certificate> content
    )
    {
        var encoded = TwicCredentialEvidenceCodec.Encode(
            input.Request.ProfileId,
            acquired.Generation,
            acquired.SignedChuid,
            authentication,
            content.Select(value => value.GetEncoded().ToImmutableArray()).ToImmutableArray(),
            acquired.EncryptedFingerprint
        );
        return WorkstationVerificationEvidence.Request(
            encoded,
            input.Request.ProfileId,
            CredentialVerificationWorkflow.Twic,
            input.Bundle,
            confidence.Uncertainty,
            clock.GetUtcNow()
        );
    }

    private Result<ImmutableArray<X509Certificate>, VerificationError> ContentPath(
        ImmutableArray<byte> chuid,
        ImmutableArray<ImmutableArray<byte>> intermediates,
        VerifiedBundleSnapshot bundle
    )
    {
        var signer = ChuidCertificateCodec.ExtractSignerCertificate(chuid);
        if (signer.IsFailure)
            return WorkstationVerificationEvidence.Failure<ImmutableArray<X509Certificate>>();
        var path = CredentialCertificatePathBuilder.Build(
            signer.Value,
            intermediates,
            bundle,
            clock.GetUtcNow()
        );
        return path.IsFailure
            ? Result.Failure<ImmutableArray<X509Certificate>, VerificationError>(path.Error)
            : path
                .Value.Select(value => new X509CertificateParser().ReadCertificate(value.ToArray()))
                .ToImmutableArray();
    }

    private static Result<CredentialVerificationResult, VerificationError> Failed() =>
        WorkstationVerificationEvidence.Failure<CredentialVerificationResult>();
}
