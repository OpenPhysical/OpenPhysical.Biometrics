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

internal sealed class TwicLivePreflight(
    CredentialCeremonyService authority,
    TwicAcceptanceService verifier
)
{
    internal async Task<Maybe<Result<CredentialVerificationResult, VerificationError>>> CheckAsync(
        TwicLiveSession live,
        CredentialVerificationRequest current,
        CancellationToken token
    )
    {
        var preflight = await verifier
            .VerifyAsync(current, live.Profile, live.Evidence(current, false), token)
            .ConfigureAwait(false);
        if (!live.IsCurrent)
            return Maybe.From(Failure("twic.session-changed"));
        if (preflight.IsFailure)
            return Maybe.From(preflight);
        var crypto = preflight.Value.Cryptographic;
        bool expected = live.Profile.RequireCardAuthentication
            ? crypto.State == VerificationCheckState.Missing
                && crypto.ReasonCodes.Length == 1
                && crypto.ReasonCodes[0] == "twic.card-proof-missing"
            : crypto.State == VerificationCheckState.Passed;
        if (
            !expected
            || !Allows(preflight.Value.Policy)
            || !Allows(preflight.Value.Trust)
            || !Allows(preflight.Value.Status)
        )
            return Maybe.From(preflight);
        if (!live.Profile.RequireCardAuthentication)
            return Maybe<Result<CredentialVerificationResult, VerificationError>>.None;
        var initial = live.Prove(current, token);
        if (initial.IsFailure)
            return Maybe.From(
                Result.Failure<CredentialVerificationResult, VerificationError>(initial.Error)
            );
        return ConsumeInitial(initial.Value, live);
    }

    private Maybe<Result<CredentialVerificationResult, VerificationError>> ConsumeInitial(
        CredentialVerificationRequest initial,
        TwicLiveSession live
    )
    {
        var hash = Convert.ToHexStringLower(
            OpenPhysical.Directory.Contracts.DirectoryDigest.Sha256(
                live.Envelope.AuthenticationChain[0].GetEncoded()
            )
        );
        return authority.Consume(initial, hash).HasNoValue
            ? Maybe.From(Failure("twic.card-proof-unavailable"))
            : Maybe<Result<CredentialVerificationResult, VerificationError>>.None;
    }

    private static bool Allows(VerificationCheck check) =>
        check.State != VerificationCheckState.Failed
        && (!check.Required || check.State == VerificationCheckState.Passed);

    private static Result<CredentialVerificationResult, VerificationError> Failure(string reason) =>
        Result.Failure<CredentialVerificationResult, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, reason)
        );
}
