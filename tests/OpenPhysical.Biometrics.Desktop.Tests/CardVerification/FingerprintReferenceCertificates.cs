// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Testing.Piv;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Tests;

internal sealed record FingerprintReferenceCertificates(
    AsymmetricCipherKeyPair RootKeys,
    AsymmetricCipherKeyPair AuthenticationKeys,
    AsymmetricCipherKeyPair ContentKeys,
    X509Certificate Root,
    X509Certificate Authentication,
    X509Certificate Content
)
{
    internal static FingerprintReferenceCertificates Create(byte[] fascn, bool twic = false)
    {
        var rootKeys = VerificationBundleFixture.NewSigner().Keys;
        var authKeys = VerificationBundleFixture.NewSigner().Keys;
        var contentKeys = VerificationBundleFixture.NewSigner().Keys;
        var root = CeremonyCertificateFixture.FingerprintCertificate(
            rootKeys.Public,
            rootKeys.Private,
            "CN=Root",
            true,
            false,
            fascn
        );
        var authentication = Leaf(authKeys, rootKeys, fascn, false, twic);
        var content = Leaf(contentKeys, rootKeys, fascn, true, twic);
        return new(rootKeys, authKeys, contentKeys, root, authentication, content);
    }

    private static X509Certificate Leaf(
        AsymmetricCipherKeyPair keys,
        AsymmetricCipherKeyPair root,
        byte[] fascn,
        bool content,
        bool twic
    ) =>
        CeremonyCertificateFixture.FingerprintCertificate(
            keys.Public,
            root.Private,
            content ? "CN=Content" : "CN=Authentication",
            false,
            content,
            fascn,
            twic: twic
        );
}
