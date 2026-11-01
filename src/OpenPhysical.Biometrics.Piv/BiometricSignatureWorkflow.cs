// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Identifies an external signing workflow failure.</summary>
public enum SignatureFailure
{
    /// <summary>The object was completed successfully.</summary>
    None,

    /// <summary>The provider returned an empty or oversized signature block.</summary>
    InvalidBlockLength,

    /// <summary>The signature length remained variable through the attempt limit.</summary>
    LengthDidNotConverge,

    /// <summary>The external verifier rejected the signature or its required envelope attributes.</summary>
    VerificationFailed,

    /// <summary>The authenticated identity differs from the requested identity.</summary>
    IdentityMismatch,
}

/// <summary>Separates the external cryptographic findings from biometric identity binding.</summary>
/// <param name="SignatureFindings">The findings supplied by the caller's verification service.</param>
/// <param name="IdentityBinding">Whether the authenticated attributes match the CBEFF header and expected card identity.</param>
public sealed record BiometricVerificationReport(
    BiometricSignatureFindings SignatureFindings,
    ValidationStatus IdentityBinding
);

/// <summary>Contains a completed object or an explicit expected signing failure.</summary>
public sealed class BiometricSigningResult
{
    internal BiometricSigningResult(
        SignatureFailure failure,
        CbeffRecord? record,
        BiometricVerificationReport? verification
    )
    {
        Failure = failure;
        Record = record;
        Verification = verification;
    }

    /// <summary>Gets the outcome of producing a self-consistent signed object.</summary>
    public SignatureFailure Failure { get; }

    /// <summary>Gets whether signing and external integrity verification completed.</summary>
    public bool IsSuccess => Failure == SignatureFailure.None;

    /// <summary>Gets the completed record when signing succeeds.</summary>
    public CbeffRecord? Record { get; }

    /// <summary>Gets verification findings when the external verifier was called.</summary>
    public BiometricVerificationReport? Verification { get; }
}

/// <summary>Coordinates exact CBEFF signing content with caller-supplied cryptographic services.</summary>
public static class BiometricSignatureWorkflow
{
    /// <summary>Gets the maximum number of signatures requested to resolve the encoded block length.</summary>
    public const int MaximumSigningAttempts = 32;

    /// <summary>Prepares owned exact signed bytes from a CBEFF record.</summary>
    /// <param name="record">The record with its declared final signature-block length.</param>
    /// <param name="identity">The expected identity, whose FASC-N must match the header.</param>
    /// <returns>An independent request containing the exact header and biometric bytes.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The header FASC-N differs from the expected identity.</exception>
    public static BiometricSignatureRequest Prepare(CbeffRecord record, BiometricIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(identity);
        if (!record.Header.FascN.AsSpan().SequenceEqual(identity.FascN.AsSpan()))
            throw new ArgumentException(
                "The CBEFF header and expected identity must agree.",
                nameof(identity)
            );
        return new BiometricSignatureRequest(CbeffCodec.GetSignedData(record), identity);
    }

    /// <summary>Attaches an owned detached block after checking the declared CBEFF length.</summary>
    /// <param name="record">The record containing the exact header previously submitted for signing.</param>
    /// <param name="signatureBlock">The block returned by the external service.</param>
    /// <returns>A new record retaining the original header and biometric data.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    /// <exception cref="ArgumentException">The block length differs from the signed header.</exception>
    public static CbeffRecord Attach(CbeffRecord record, ReadOnlySpan<byte> signatureBlock)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (signatureBlock.IsEmpty || signatureBlock.Length != record.Header.SignatureLength)
            throw new ArgumentException(
                "The signature block must match the signed header length.",
                nameof(signatureBlock)
            );
        return new CbeffRecord(
            record.Header,
            record.BiometricData,
            ImmutableArray.Create(signatureBlock.ToArray())
        );
    }

    /// <summary>Resolves the encoded length, attaches the detached block, and requests external verification.</summary>
    /// <param name="record">Biometric content and metadata to sign.</param>
    /// <param name="identity">The expected card identity.</param>
    /// <param name="signer">The caller's CMS signing service.</param>
    /// <param name="verifier">The caller's CMS verification service.</param>
    /// <param name="cancellationToken">Cancellation for external operations and between attempts.</param>
    /// <returns>A completed object or a typed signing outcome.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static async Task<BiometricSigningResult> SignAsync(
        CbeffRecord record,
        BiometricIdentity identity,
        IBiometricSignatureProvider signer,
        IBiometricSignatureVerifier verifier,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(verifier);
        _ = Prepare(record, identity);
        var length = record.Header.SignatureLength;
        for (var attempt = 0; attempt < MaximumSigningAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The block length is itself signed. Every length change therefore
            // requires a fresh signing request covering the updated header.
            var candidate = new CbeffRecord(
                record.Header.WithSignatureLength(length),
                record.BiometricData,
                ImmutableArray<byte>.Empty
            );
            var request = Prepare(candidate, identity);
            var block = await signer.SignAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (block.IsDefaultOrEmpty || block.Length > ushort.MaxValue)
                return new BiometricSigningResult(SignatureFailure.InvalidBlockLength, null, null);
            if (block.Length != length)
            {
                length = (ushort)block.Length;
                continue;
            }

            var completed = Attach(candidate, block.AsSpan());
            var verification = await VerifyAsync(completed, identity, verifier, cancellationToken)
                .ConfigureAwait(false);
            if (
                verification.SignatureFindings.Signature != ValidationStatus.Passed
                || verification.SignatureFindings.Envelope != ValidationStatus.Passed
            )
                return new BiometricSigningResult(
                    SignatureFailure.VerificationFailed,
                    null,
                    verification
                );
            if (verification.IdentityBinding != ValidationStatus.Passed)
                return new BiometricSigningResult(
                    SignatureFailure.IdentityMismatch,
                    null,
                    verification
                );
            return new BiometricSigningResult(SignatureFailure.None, completed, verification);
        }
        return new BiometricSigningResult(SignatureFailure.LengthDidNotConverge, null, null);
    }

    /// <summary>Requests verification over original content and evaluates biometric identity binding.</summary>
    /// <param name="record">The parsed record retaining original header and biometric bytes.</param>
    /// <param name="identity">Expected identity supplied by the card/application layer.</param>
    /// <param name="verifier">The caller's signature and trust service.</param>
    /// <param name="cancellationToken">Cancellation for external operations.</param>
    /// <returns>Separate signature-service findings and biometric identity binding status.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">Signature and header lengths differ.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static async Task<BiometricVerificationReport> VerifyAsync(
        CbeffRecord record,
        BiometricIdentity identity,
        IBiometricSignatureVerifier verifier,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(verifier);
        cancellationToken.ThrowIfCancellationRequested();
        if (
            record.Signature.IsDefaultOrEmpty
            || record.Signature.Length != record.Header.SignatureLength
        )
            throw new ArgumentException(
                "The signature block must match its declared length.",
                nameof(record)
            );
        // Keep untrusted header identity separate from the expected card identity
        // until the external service authenticates the original signed content.
        var request = new BiometricSignatureRequest(CbeffCodec.GetSignedData(record), identity);
        var findings = await verifier
            .VerifyAsync(request, record.Signature, cancellationToken)
            .ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(findings);
        cancellationToken.ThrowIfCancellationRequested();
        var binding =
            findings.Signature != ValidationStatus.Passed
            || findings.Envelope != ValidationStatus.Passed
                ? ValidationStatus.NotEvaluated
            : findings.VerifiedIdentity is not null
            && findings.VerifiedIdentity.Equals(identity)
            && record.Header.FascN.AsSpan().SequenceEqual(identity.FascN.AsSpan())
                ? ValidationStatus.Passed
            : ValidationStatus.Failed;
        return new BiometricVerificationReport(findings, binding);
    }
}
