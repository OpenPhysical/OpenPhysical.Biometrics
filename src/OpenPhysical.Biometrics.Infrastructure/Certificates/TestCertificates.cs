namespace OpenPhysical.Biometrics.Infrastructure.Certificates;

using System;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

/// <summary>
/// Provides deterministic test certificates for unit testing.
/// </summary>
public static class TestCertificates
{
    private static readonly Lazy<CertificateSet> _certificates = new Lazy<CertificateSet>(CreateCertificates);

    /// <summary>
    /// Gets the test root CA certificate.
    /// </summary>
    public static X509Certificate RootCA => _certificates.Value.RootCA;

    /// <summary>
    /// Gets the test intermediate CA certificate.
    /// </summary>
    public static X509Certificate IntermediateCA => _certificates.Value.IntermediateCA;

    /// <summary>
    /// Gets the test content signer certificate.
    /// </summary>
    public static X509Certificate ContentSigner => _certificates.Value.ContentSigner;

    /// <summary>
    /// Gets the root CA key pair.
    /// </summary>
    public static AsymmetricCipherKeyPair RootKeyPair => _certificates.Value.RootKeyPair;

    /// <summary>
    /// Gets the intermediate CA key pair.
    /// </summary>
    public static AsymmetricCipherKeyPair IntermediateKeyPair => _certificates.Value.IntermediateKeyPair;

    /// <summary>
    /// Gets the content signer key pair.
    /// </summary>
    public static AsymmetricCipherKeyPair ContentSignerKeyPair => _certificates.Value.ContentSignerKeyPair;

    private class CertificateSet
    {
        public X509Certificate RootCA { get; set; } = null!;
        public X509Certificate IntermediateCA { get; set; } = null!;
        public X509Certificate ContentSigner { get; set; } = null!;
        public AsymmetricCipherKeyPair RootKeyPair { get; set; } = null!;
        public AsymmetricCipherKeyPair IntermediateKeyPair { get; set; } = null!;
        public AsymmetricCipherKeyPair ContentSignerKeyPair { get; set; } = null!;
    }

    private static CertificateSet CreateCertificates()
    {
        var set = new CertificateSet();

        // Generate key pairs
        set.RootKeyPair = GenerateDeterministicKeyPair(1);
        set.IntermediateKeyPair = GenerateDeterministicKeyPair(2);
        set.ContentSignerKeyPair = GenerateDeterministicKeyPair(3);

        // Build Root CA (self-signed)
        set.RootCA = BuildRootCA(set.RootKeyPair);

        // Build Intermediate CA (signed by Root)
        set.IntermediateCA = BuildIntermediateCA(set.IntermediateKeyPair, set.RootKeyPair, set.RootCA);

        // Build Content Signer (signed by Intermediate)
        set.ContentSigner = BuildContentSigner(set.ContentSignerKeyPair, set.IntermediateKeyPair, set.IntermediateCA);

        return set;
    }

    private static X509Certificate BuildRootCA(AsymmetricCipherKeyPair keyPair)
    {
        var certGen = new X509V3CertificateGenerator();
        
        // Fixed serial number for deterministic testing
        var serialNumber = BigInteger.One;
        var issuerDN = new X509Name("CN=Test Root CA, O=OpenPhysical Test, C=US");
        var subjectDN = issuerDN; // Self-signed

        certGen.SetSerialNumber(serialNumber);
        certGen.SetIssuerDN(issuerDN);
        certGen.SetSubjectDN(subjectDN);
        
        // Fixed dates for deterministic testing
        certGen.SetNotBefore(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        certGen.SetNotAfter(new DateTime(2034, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        certGen.SetPublicKey(keyPair.Public);

        // Add basic constraints for CA
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        certGen.AddExtension(X509Extensions.KeyUsage, true, 
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        // Sign with private key
        var signatureFactory = new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory(
            "SHA256WithRSA", keyPair.Private);
        return certGen.Generate(signatureFactory);
    }

    private static X509Certificate BuildIntermediateCA(
        AsymmetricCipherKeyPair keyPair, 
        AsymmetricCipherKeyPair issuerKeyPair,
        X509Certificate issuerCert)
    {
        var certGen = new X509V3CertificateGenerator();
        
        // Fixed serial number for deterministic testing
        var serialNumber = BigInteger.Two;
        var issuerDN = issuerCert.SubjectDN;
        var subjectDN = new X509Name("CN=Test Intermediate CA, O=OpenPhysical Test, C=US");

        certGen.SetSerialNumber(serialNumber);
        certGen.SetIssuerDN(issuerDN);
        certGen.SetSubjectDN(subjectDN);
        
        // Fixed dates for deterministic testing
        certGen.SetNotBefore(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        certGen.SetNotAfter(new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        certGen.SetPublicKey(keyPair.Public);

        // Add basic constraints for intermediate CA
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(0));
        certGen.AddExtension(X509Extensions.KeyUsage, true, 
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        // Sign with issuer's private key
        var signatureFactory = new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory(
            "SHA256WithRSA", issuerKeyPair.Private);
        return certGen.Generate(signatureFactory);
    }

    private static X509Certificate BuildContentSigner(
        AsymmetricCipherKeyPair keyPair,
        AsymmetricCipherKeyPair issuerKeyPair,
        X509Certificate issuerCert)
    {
        var certGen = new X509V3CertificateGenerator();
        
        // Fixed serial number for deterministic testing
        var serialNumber = BigInteger.Three;
        var issuerDN = issuerCert.SubjectDN;
        var subjectDN = new X509Name("CN=Test Content Signer, O=OpenPhysical Test, C=US");

        certGen.SetSerialNumber(serialNumber);
        certGen.SetIssuerDN(issuerDN);
        certGen.SetSubjectDN(subjectDN);
        
        // Fixed dates for deterministic testing
        certGen.SetNotBefore(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        certGen.SetNotAfter(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        certGen.SetPublicKey(keyPair.Public);

        // Add basic constraints for end entity
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
        certGen.AddExtension(X509Extensions.KeyUsage, true, 
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.NonRepudiation));

        // Sign with issuer's private key
        var signatureFactory = new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory(
            "SHA256WithRSA", issuerKeyPair.Private);
        return certGen.Generate(signatureFactory);
    }

    private static AsymmetricCipherKeyPair GenerateDeterministicKeyPair(int seed)
    {
        // Use a deterministic random generator with a fixed seed
        var seedBytes = new byte[32];
        // Fill with a deterministic pattern based on seed
        for (int i = 0; i < seedBytes.Length; i++)
        {
            seedBytes[i] = (byte)((seed * 31 + i) % 256);
        }
        
        var secureRandom = new SecureRandom();
        secureRandom.SetSeed(seedBytes);
        
        // Generate RSA key pair
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(new RsaKeyGenerationParameters(
            BigInteger.ValueOf(65537), 
            secureRandom, 
            2048, 
            25));
        
        return keyGen.GenerateKeyPair();
    }
}