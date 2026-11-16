// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;

public static partial class StationAgreementHpke
{
    /// <summary>Encrypts exactly one 32-byte challenge using the fixed P-256/HKDF-SHA256/AES-256-GCM suite.</summary>
    /// <param name="recipientSubjectPublicKeyInfo">Canonical DER P-256 SubjectPublicKeyInfo.</param>
    /// <param name="info">Bounded application-specific HPKE info.</param>
    /// <param name="aad">Bounded application-specific authenticated data.</param>
    /// <param name="challenge">Exactly 32 unpredictable challenge bytes.</param>
    /// <returns>The ephemeral point and 48-byte authenticated ciphertext.</returns>
    public static Result<StationAgreementHpkeCiphertext, PackageError> SealBase(
        ReadOnlySpan<byte> recipientSubjectPublicKeyInfo,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> challenge
    )
    {
        if (!ValidLengths(recipientSubjectPublicKeyInfo, info, aad, challenge))
            return InvalidInputs();

        var inputs = new byte[4][];
        try
        {
            inputs[0] = recipientSubjectPublicKeyInfo.ToArray();
            inputs[1] = info.ToArray();
            inputs[2] = aad.ToArray();
            inputs[3] = challenge.ToArray();
            var validKey = PackageEdge.Try(
                () => ValidPublicKey(inputs[0]),
                PackageErrorCodes.InvalidEnvelope,
                "The station challenge inputs are invalid."
            );
            if (validKey.IsFailure || !validKey.Value)
                return InvalidInputs();

            return PackageEdge.Try(
                () => SealCore(inputs),
                PackageErrorCodes.CryptoFailed,
                "The station challenge could not be encrypted."
            );
        }
        finally
        {
            Clear(inputs);
        }
    }

    private static bool ValidLengths(
        ReadOnlySpan<byte> spki,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> challenge
    ) =>
        spki.Length == SpkiLength
        && info.Length is > 0 and <= MaximumInfoLength
        && aad.Length is > 0 and <= MaximumAadLength
        && challenge.Length == ChallengeLength;

    private static StationAgreementHpkeCiphertext SealCore(byte[][] input)
    {
        var result = EnrollmentEnvelopeCrypto.Seal(input[0], input[1], input[2], input[3]);
        return new StationAgreementHpkeCiphertext(result.Encapsulation, result.Ciphertext);
    }
}
