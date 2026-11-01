// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;

// Resolves the encoded signature length, one signing request per candidate length.
internal static class BiometricSigningAttempts
{
    internal static async Task<Result<BiometricSigningResult, PivError>> RunAsync(
        BiometricSigningContext context,
        ushort length,
        int attempt
    )
    {
        if (attempt >= BiometricSignatureWorkflow.MaximumSigningAttempts)
            return Outcome(SignatureFailure.LengthDidNotConverge);
        var candidate = context.Candidate(length);
        var signed = await SignAsync(context, candidate).ConfigureAwait(false);
        if (signed.IsFailure)
            return Result.Failure<BiometricSigningResult, PivError>(signed.Error);
        var block = signed.Value;
        if (block.IsDefaultOrEmpty || block.Length > ushort.MaxValue)
            return Outcome(SignatureFailure.InvalidBlockLength);
        return block.Length != length
            ? await RunAsync(context, (ushort)block.Length, attempt + 1).ConfigureAwait(false)
            : await CompleteAsync(context, candidate, block).ConfigureAwait(false);
    }

    private static async Task<Result<ImmutableArray<byte>, PivError>> SignAsync(
        BiometricSigningContext context,
        CbeffRecord candidate
    )
    {
        var request = BiometricSignatureWorkflow.Prepare(candidate, context.Identity);
        return request.IsFailure
            ? Result.Failure<ImmutableArray<byte>, PivError>(request.Error)
            : await ProviderEdge
                .CallAsync(
                    () => context.Signer.SignAsync(request.Value, context.CancellationToken),
                    context.CancellationToken,
                    PivErrorCodes.SigningProviderFailed
                )
                .ConfigureAwait(false);
    }

    private static async Task<Result<BiometricSigningResult, PivError>> CompleteAsync(
        BiometricSigningContext context,
        CbeffRecord candidate,
        ImmutableArray<byte> block
    )
    {
        var completed = BiometricSignatureWorkflow.Attach(candidate, block.AsSpan());
        if (completed.IsFailure)
            return Result.Failure<BiometricSigningResult, PivError>(completed.Error);
        var verification = await BiometricSignatureWorkflow
            .VerifyAsync(
                completed.Value,
                context.Identity,
                context.Verifier,
                context.CancellationToken
            )
            .ConfigureAwait(false);
        return verification.Map(report => Classify(completed.Value, report));
    }

    private static BiometricSigningResult Classify(
        CbeffRecord completed,
        BiometricVerificationReport report
    )
    {
        var findings = report.SignatureFindings;
        if (
            findings.Signature != ValidationStatus.Passed
            || findings.Envelope != ValidationStatus.Passed
        )
            return new(SignatureFailure.VerificationFailed, Maybe<CbeffRecord>.None, report);
        return report.IdentityBinding != ValidationStatus.Passed
            ? new(SignatureFailure.IdentityMismatch, Maybe<CbeffRecord>.None, report)
            : new(SignatureFailure.None, completed, report);
    }

    private static Result<BiometricSigningResult, PivError> Outcome(SignatureFailure failure) =>
        new BiometricSigningResult(
            failure,
            Maybe<CbeffRecord>.None,
            Maybe<BiometricVerificationReport>.None
        );
}
