// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

/// <summary>Bounded single-shot HPKE operations for station agreement-key possession proofs.</summary>
public static partial class StationAgreementHpke
{
    private const int ChallengeLength = 32;
    private const int EncapsulationLength = 65;
    private const int CiphertextLength = 48;
    private const int SpkiLength = 91;
    private const int MaximumInfoLength = 128;
    private const int MaximumAadLength = 1_100;

    private static bool ValidPublicKey(ReadOnlySpan<byte> spki)
    {
        if (spki.Length != SpkiLength)
            return false;

        var parsed = PublicKeyFactory.CreateKey(spki.ToArray()) as ECPublicKeyParameters;
        if (parsed is null)
            return false;

        EnrollmentHpkeSchedule.RequireCurve(parsed.Parameters);
        EnrollmentHpkeSchedule.ValidatePublic(parsed.Q, parsed.Parameters);
        var canonicalSpki = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(parsed)
            .GetDerEncoded();
        return parsed.Q.GetEncoded(false).Length == EncapsulationLength
            && canonicalSpki.AsSpan().SequenceEqual(spki);
    }

    private static Result<StationAgreementHpkeCiphertext, PackageError> InvalidInputs() =>
        new PackageError(
            PackageErrorCodes.InvalidEnvelope,
            "The station challenge inputs are invalid."
        );

    private static Result<byte[], PackageError> InvalidOpen() =>
        new PackageError(
            PackageErrorCodes.InvalidEnvelope,
            "The station challenge inputs are invalid."
        );

    private static void Clear(params byte[][] buffers)
    {
        foreach (var buffer in buffers)
        {
            if (buffer is not null)
                Array.Clear(buffer);
        }
    }
}
