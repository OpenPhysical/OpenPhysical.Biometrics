// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using Org.BouncyCastle.Asn1;

internal static class EnrollmentEnvelopeMutationFixtures
{
    internal static byte[] SwapArtifactCiphertextsAndResign(
        byte[] encoded,
        IEnrollmentSigner signer
    )
    {
        var root = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(encoded));
        var original = Asn1Sequence.GetInstance(root[1]);
        var files = new Asn1Encodable[original.Count];
        for (var i = 0; i < files.Length; i++)
        {
            var source = Asn1Sequence.GetInstance(
                original[
                    i switch
                    {
                        1 => 2,
                        2 => 1,
                        _ => i,
                    }
                ]
            );
            files[i] = new DerSequence(
                DerInteger.ValueOf(i),
                Asn1OctetString.GetInstance(source[1]),
                Asn1OctetString.GetInstance(source[2])
            );
        }
        return Resign(root[0], new DerSequence(files), signer);
    }

    internal static byte[] ReplaceFirstEncapsulationAndResign(
        byte[] encoded,
        IEnrollmentSigner signer,
        byte[] encapsulation
    )
    {
        var root = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(encoded));
        var files = Asn1Sequence.GetInstance(root[1]).ToArray();
        var first = Asn1Sequence.GetInstance(files[0]);
        files[0] = new DerSequence(
            DerInteger.ValueOf(0),
            new DerOctetString(encapsulation),
            first[2]
        );
        return Resign(root[0], new DerSequence(files), signer);
    }

    private static byte[] Resign(Asn1Encodable header, DerSequence files, IEnrollmentSigner signer)
    {
        var signed = new DerSequence(header, files).GetEncoded(Asn1Encodable.Der);
        var digest = EnrollmentOriginArtifactCodec.Digest([
            .. "OpenPhysical.EnrollmentEnvelope.v2\0"u8,
            .. signed,
        ]);
        var signature = signer.SignDigest(digest).Value;
        Array.Clear(digest);
        return new DerSequence(header, files, new DerOctetString(signature)).GetEncoded(
            Asn1Encodable.Der
        );
    }
}
