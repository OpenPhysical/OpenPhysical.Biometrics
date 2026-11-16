// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Math;

internal static class EnrollmentDerIntegers
{
    internal static bool TryUInt(Asn1Encodable encoded, out uint value)
    {
        var integer = DerInteger.GetInstance(encoded).Value;
        if (integer.SignValue < 0 || integer.BitLength > 32)
        {
            value = 0;
            return false;
        }
        value = (uint)integer.LongValue;
        return true;
    }

    internal static bool TryPositiveLong(Asn1Encodable encoded, out long value)
    {
        var integer = DerInteger.GetInstance(encoded).Value;
        if (integer.SignValue <= 0 || integer.BitLength > 63)
        {
            value = 0;
            return false;
        }
        value = integer.LongValue;
        return true;
    }

    internal static bool TryBoundedInt(
        Asn1Encodable encoded,
        int minimum,
        int maximum,
        out int value
    )
    {
        var integer = DerInteger.GetInstance(encoded).Value;
        if (
            integer.SignValue < 0
            || integer.BitLength > 31
            || integer.CompareTo(BigInteger.ValueOf(minimum)) < 0
            || integer.CompareTo(BigInteger.ValueOf(maximum)) > 0
        )
        {
            value = 0;
            return false;
        }
        value = integer.IntValue;
        return true;
    }

    internal static bool Equals(Asn1Encodable encoded, int expected) =>
        TryBoundedInt(encoded, expected, expected, out _);
}
