// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

/// <summary>Fixture-only software signing and ECDH adapter; production code has no fallback.</summary>
internal sealed class EnrollmentEnvelopeTestKeys : IEnrollmentSigner, IEnrollmentRecipientKey
{
    private static readonly X9ECParameters Curve = ECNamedCurveTable.GetByName("secp256r1");
    private static readonly ECDomainParameters Domain = new(
        Curve.Curve,
        Curve.G,
        Curve.N,
        Curve.H,
        Curve.GetSeed()
    );
    private readonly AsymmetricCipherKeyPair pair;
    private int derivationCount;

    internal EnrollmentEnvelopeTestKeys()
    {
        var generator = new ECKeyPairGenerator("ECDSA");
        generator.Init(new ECKeyGenerationParameters(Domain, new SecureRandom()));
        pair = generator.GenerateKeyPair();
        SubjectPublicKeyInfo = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(pair.Public)
            .GetDerEncoded();
        KeyId = Hash(SubjectPublicKeyInfo.ToArray());
    }

    public ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; }
    public ReadOnlyMemory<byte> KeyId { get; }
    internal int DerivationCount => derivationCount;
    internal int SigningCount { get; private set; }

    public Result<byte[], PackageError> SignDigest(ReadOnlySpan<byte> sha256Digest)
    {
        SigningCount++;
        if (sha256Digest.Length != 32)
            return new PackageError(PackageErrorCodes.CryptoFailed, "Expected SHA-256 digest.");
        var signer = new ECDsaSigner();
        signer.Init(true, pair.Private);
        var values = signer.GenerateSignature(sha256Digest.ToArray());
        return new DerSequence(new DerInteger(values[0]), new DerInteger(values[1])).GetEncoded(
            Asn1Encodable.Der
        );
    }

    public Result<byte[], PackageError> DeriveSharedSecret(
        ReadOnlySpan<byte> ephemeralUncompressedPoint
    )
    {
        Interlocked.Increment(ref derivationCount);
        var point = Domain.Curve.DecodePoint(ephemeralUncompressedPoint.ToArray());
        var agreement = new ECDHBasicAgreement();
        agreement.Init(pair.Private);
        return Org.BouncyCastle.Utilities.BigIntegers.AsUnsignedByteArray(
            32,
            agreement.CalculateAgreement(new ECPublicKeyParameters("ECDH", point, Domain))
        );
    }

    private static byte[] Hash(byte[] input)
    {
        var hash = new Sha256Digest();
        hash.BlockUpdate(input);
        var result = new byte[32];
        hash.DoFinal(result);
        return result;
    }
}

internal sealed class EnrollmentEnvelopeTestResolver : IEnrollmentKeyResolver
{
    private readonly Dictionary<string, byte[]> keys = new(StringComparer.Ordinal);

    internal void Add(IEnrollmentSigner signer) =>
        keys[Convert.ToHexString(signer.KeyId.Span)] = signer.SubjectPublicKeyInfo.ToArray();

    public Result<byte[], PackageError> ResolveSubjectPublicKeyInfo(ReadOnlySpan<byte> keyId) =>
        keys.TryGetValue(Convert.ToHexString(keyId), out var key)
            ? key.ToArray()
            : new PackageError(PackageErrorCodes.KeyMismatch, "Test key is not registered.");
}

internal sealed class EnrollmentEnvelopeMalformedRecipient : IEnrollmentRecipientKey
{
    internal EnrollmentEnvelopeMalformedRecipient(EnrollmentEnvelopeTestKeys identity)
    {
        SubjectPublicKeyInfo = identity.SubjectPublicKeyInfo;
        KeyId = identity.KeyId;
    }

    internal byte[] RawSecret { get; } = new byte[31];
    public ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; }
    public ReadOnlyMemory<byte> KeyId { get; }

    public Result<byte[], PackageError> DeriveSharedSecret(
        ReadOnlySpan<byte> ephemeralUncompressedPoint
    ) => RawSecret;
}
