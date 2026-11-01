// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Records the outcome of one independently evaluated check.</summary>
public enum ValidationStatus
{
    /// <summary>The check awaits evaluation by the responsible service.</summary>
    NotEvaluated,

    /// <summary>The check passed.</summary>
    Passed,

    /// <summary>The check failed.</summary>
    Failed,
}

/// <summary>Identifies a credential using the values bound into PIV biometric signatures.</summary>
public sealed class BiometricIdentity : IEquatable<BiometricIdentity>
{
    /// <summary>Creates an owned, validated identity.</summary>
    /// <param name="fascN">The 25-byte encoded FASC-N.</param>
    /// <param name="cardUuid">The card UUID from the credential's CHUID.</param>
    /// <exception cref="ArgumentException">The FASC-N length or UUID is invalid.</exception>
    public BiometricIdentity(ReadOnlySpan<byte> fascN, Guid cardUuid)
    {
        if (fascN.Length != 25)
            throw new ArgumentException("A PIV FASC-N contains 25 bytes.", nameof(fascN));
        if (cardUuid == Guid.Empty)
            throw new ArgumentException("A card UUID must be specified.", nameof(cardUuid));
        FascN = ImmutableArray.Create(fascN.ToArray());
        CardUuid = cardUuid;
    }

    /// <summary>Gets the owned encoded FASC-N.</summary>
    public ImmutableArray<byte> FascN { get; }

    /// <summary>Gets the card UUID.</summary>
    public Guid CardUuid { get; }

    /// <summary>Compares the two encoded identity values.</summary>
    /// <param name="other">The identity to compare.</param>
    /// <returns>Whether both values match.</returns>
    public bool Equals(BiometricIdentity? other) =>
        other is not null
        && CardUuid == other.CardUuid
        && FascN.AsSpan().SequenceEqual(other.FascN.AsSpan());

    /// <summary>Compares another object with this identity.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether the object is an equal identity.</returns>
    public override bool Equals(object? obj) => obj is BiometricIdentity other && Equals(other);

    /// <summary>Computes a hash suitable for in-process identity lookup.</summary>
    /// <returns>The combined hash.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CardUuid);
        foreach (var value in FascN)
            hash.Add(value);
        return hash.ToHashCode();
    }
}

/// <summary>Describes the PIV-specific envelope and signed-attribute requirements for an external service.</summary>
public sealed class BiometricSignatureRequirements
{
    /// <summary>Gets the PIV biometric-object content type.</summary>
    public string ContentTypeOid => "2.16.840.1.101.3.6.2";

    /// <summary>Gets the PIV FASC-N signed-attribute identifier.</summary>
    public string FascNAttributeOid => "2.16.840.1.101.3.6.6";

    /// <summary>Gets the entryUUID signed-attribute identifier.</summary>
    public string EntryUuidAttributeOid => "1.3.6.1.1.16.4";

    /// <summary>Gets the PIV signer distinguished-name attribute identifier.</summary>
    public string SignerDnAttributeOid => "2.16.840.1.101.3.6.5";

    /// <summary>Gets the required message-digest signed-attribute identifier.</summary>
    public string MessageDigestAttributeOid => "1.2.840.113549.1.9.4";

    /// <summary>Gets whether CMS identifies the signer by certificate issuer and serial number.</summary>
    public bool UseIssuerAndSerialNumber => true;

    /// <summary>Gets whether the CMS envelope may include a CRL collection.</summary>
    public bool AllowCrls => false;

    /// <summary>Gets the required number of signers.</summary>
    public int SignerCount => 1;

    /// <summary>Gets whether the content is detached.</summary>
    public bool Detached => true;

    /// <summary>Gets the required CMS SignedData version.</summary>
    public int SignedDataVersion => 3;
}

/// <summary>Contains owned exact content bytes and the identity expected in its signature.</summary>
public sealed class BiometricSignatureRequest
{
    /// <summary>Creates a request retaining an independent copy of its content.</summary>
    /// <param name="signedContent">The original CBEFF header followed by the biometric record.</param>
    /// <param name="identity">The identity expected in the signed attributes.</param>
    /// <exception cref="ArgumentException">The content is shorter than a CBEFF header.</exception>
    /// <exception cref="ArgumentNullException">The identity is null.</exception>
    public BiometricSignatureRequest(ReadOnlySpan<byte> signedContent, BiometricIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (signedContent.Length < CbeffHeader.Size)
            throw new ArgumentException(
                $"Signed content includes the {CbeffHeader.Size}-byte CBEFF header.",
                nameof(signedContent)
            );
        SignedContent = ImmutableArray.Create(signedContent.ToArray());
        Identity = identity;
    }

    /// <summary>Gets the exact bytes the external service signs or verifies.</summary>
    public ImmutableArray<byte> SignedContent { get; }

    /// <summary>Gets the expected credential identity.</summary>
    public BiometricIdentity Identity { get; }

    /// <summary>Gets the biometric-specific requirements for the external signature service.</summary>
    public BiometricSignatureRequirements Requirements { get; } = new();
}

/// <summary>Contains the results supplied by the external signature and trust services.</summary>
/// <param name="Signature">Signature-integrity status.</param>
/// <param name="Envelope">CMS profile and signed-attribute conformance status.</param>
/// <param name="Trust">Certificate-path trust status.</param>
/// <param name="Revocation">Certificate-revocation status.</param>
/// <param name="VerifiedIdentity">Identity authenticated by the signature, when available.</param>
public sealed record BiometricSignatureFindings(
    ValidationStatus Signature,
    ValidationStatus Envelope,
    ValidationStatus Trust,
    ValidationStatus Revocation,
    BiometricIdentity? VerifiedIdentity
);

/// <summary>Supplies CMS construction and cryptographic signing on behalf of the caller.</summary>
public interface IBiometricSignatureProvider
{
    /// <summary>Produces an owned detached CMS block for the exact requested content.</summary>
    /// <param name="request">Content and biometric-specific signed-attribute requirements.</param>
    /// <param name="cancellationToken">Cancellation for external signing operations.</param>
    /// <returns>The complete detached CMS block.</returns>
    Task<ImmutableArray<byte>> SignAsync(
        BiometricSignatureRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Supplies CMS interpretation, signature verification, and configured trust evaluation.</summary>
public interface IBiometricSignatureVerifier
{
    /// <summary>Evaluates the supplied signature against the exact requested content.</summary>
    /// <param name="request">Original content and expected identity.</param>
    /// <param name="signatureBlock">The owned detached CMS block.</param>
    /// <param name="cancellationToken">Cancellation for external verification and trust operations.</param>
    /// <returns>Independent signature, envelope, trust, revocation, and authenticated identity findings.</returns>
    Task<BiometricSignatureFindings> VerifyAsync(
        BiometricSignatureRequest request,
        ImmutableArray<byte> signatureBlock,
        CancellationToken cancellationToken = default
    );
}
