// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats;
using OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Coordinates exact CBEFF signing content with caller-supplied cryptographic services.</summary>
public static class BiometricSignatureWorkflow
{
    /// <summary>Gets the maximum number of signatures requested to resolve the encoded block length.</summary>
    public const int MaximumSigningAttempts = 32;

    /// <summary>Prepares owned exact signed bytes from a CBEFF record.</summary>
    /// <param name="record">The record with its declared final signature-block length.</param>
    /// <param name="identity">The expected identity, whose FASC-N must match the header.</param>
    /// <returns>An independent request containing the exact header and biometric bytes, or an error when the header FASC-N differs from the expected identity or the record is inconsistent.</returns>
    public static Result<BiometricSignatureRequest, PivError> Prepare(
        CbeffRecord record,
        BiometricIdentity identity
    ) =>
        record.Header.FascN.AsSpan().SequenceEqual(identity.FascN.AsSpan())
            ? CbeffCodec
                .GetSignedData(record)
                .MapError(ToPivError)
                .Bind(content => BiometricSignatureRequest.Create(content, identity))
            : Result.Failure<BiometricSignatureRequest, PivError>(
                new PivError(
                    PivErrorCodes.HeaderIdentityMismatch,
                    "The CBEFF header and expected identity must agree."
                )
            );

    /// <summary>Attaches an owned detached block after checking the declared CBEFF length.</summary>
    /// <param name="record">The record containing the exact header previously submitted for signing.</param>
    /// <param name="signatureBlock">The block returned by the external service.</param>
    /// <returns>A new record retaining the original header and biometric data, or an error when the block length differs from the signed header.</returns>
    public static Result<CbeffRecord, PivError> Attach(
        CbeffRecord record,
        ReadOnlySpan<byte> signatureBlock
    ) =>
        signatureBlock.IsEmpty || signatureBlock.Length != record.Header.SignatureLength
            ? Result.Failure<CbeffRecord, PivError>(
                new PivError(
                    PivErrorCodes.SignatureLength,
                    "The signature block must match the signed header length."
                )
            )
            : new CbeffRecord(
                record.Header,
                record.BiometricData,
                ImmutableArray.Create(signatureBlock.ToArray())
            );

    /// <summary>Resolves the encoded length, attaches the detached block, and requests external verification.</summary>
    /// <param name="record">Biometric content and metadata to sign.</param>
    /// <param name="identity">The expected card identity.</param>
    /// <param name="signer">The caller's CMS signing service.</param>
    /// <param name="verifier">The caller's CMS verification service.</param>
    /// <param name="cancellationToken">Cancellation for external operations and between attempts.</param>
    /// <returns>A completed object or a typed signing outcome. The result fails when the record and identity disagree, a service fails, or cancellation is requested; its code is then one of <see cref="PivErrorCodes"/>.</returns>
    public static async Task<Result<BiometricSigningResult, PivError>> SignAsync(
        CbeffRecord record,
        BiometricIdentity identity,
        IBiometricSignatureProvider signer,
        IBiometricSignatureVerifier verifier,
        CancellationToken cancellationToken = default
    )
    {
        var prepared = Prepare(record, identity);
        if (prepared.IsFailure)
            return Result.Failure<BiometricSigningResult, PivError>(prepared.Error);
        var context = new BiometricSigningContext(
            record,
            identity,
            signer,
            verifier,
            cancellationToken
        );
        return await BiometricSigningAttempts
            .RunAsync(context, record.Header.SignatureLength, 0)
            .ConfigureAwait(false);
    }

    /// <summary>Requests verification over original content and evaluates biometric identity binding.</summary>
    /// <param name="record">The parsed record retaining original header and biometric bytes.</param>
    /// <param name="identity">Expected identity supplied by the card/application layer.</param>
    /// <param name="verifier">The caller's signature and trust service.</param>
    /// <param name="cancellationToken">Cancellation for external operations.</param>
    /// <returns>Separate signature-service findings and biometric identity binding status, or an error when the lengths differ, the service fails, or cancellation is requested.</returns>
    public static async Task<Result<BiometricVerificationReport, PivError>> VerifyAsync(
        CbeffRecord record,
        BiometricIdentity identity,
        IBiometricSignatureVerifier verifier,
        CancellationToken cancellationToken = default
    )
    {
        // Keep untrusted header identity separate from the expected card identity
        // until the external service authenticates the original signed content.
        var request = DescribeVerification(record, identity);
        if (request.IsFailure)
            return Result.Failure<BiometricVerificationReport, PivError>(request.Error);
        var findings = await ProviderEdge
            .CallAsync(
                () => verifier.VerifyAsync(request.Value, record.Signature, cancellationToken),
                cancellationToken,
                PivErrorCodes.VerificationProviderFailed
            )
            .ConfigureAwait(false);
        return findings.Map(result => new BiometricVerificationReport(
            result,
            BindIdentity(record, identity, result)
        ));
    }

    private static Result<BiometricSignatureRequest, PivError> DescribeVerification(
        CbeffRecord record,
        BiometricIdentity identity
    ) =>
        record.Signature.IsDefaultOrEmpty
        || record.Signature.Length != record.Header.SignatureLength
            ? Result.Failure<BiometricSignatureRequest, PivError>(
                new PivError(
                    PivErrorCodes.SignatureLength,
                    "The signature block must match its declared length."
                )
            )
            : CbeffCodec
                .GetSignedData(record)
                .MapError(ToPivError)
                .Bind(content => BiometricSignatureRequest.Create(content, identity));

    private static ValidationStatus BindIdentity(
        CbeffRecord record,
        BiometricIdentity identity,
        BiometricSignatureFindings findings
    )
    {
        if (
            findings.Signature != ValidationStatus.Passed
            || findings.Envelope != ValidationStatus.Passed
        )
            return ValidationStatus.NotEvaluated;
        var authenticated = findings.VerifiedIdentity.Match(
            verified => verified.Equals(identity),
            () => false
        );
        return authenticated && record.Header.FascN.AsSpan().SequenceEqual(identity.FascN.AsSpan())
            ? ValidationStatus.Passed
            : ValidationStatus.Failed;
    }

    private static PivError ToPivError(FormatError error) =>
        new(PivErrorCodes.InvalidRecord, error.Message);
}
