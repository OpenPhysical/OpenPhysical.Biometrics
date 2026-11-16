// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using Org.BouncyCastle.Asn1;

internal sealed record EnrollmentEnvelopeHeader(
    string OrganizationId,
    string TransferId,
    string SenderId,
    string RecipientId,
    string EnrollmentId,
    uint Revision,
    byte[] SenderKeyId,
    byte[] RecipientKeyId,
    EnrollmentTransferPurpose Purpose,
    long CreatedUnixSeconds,
    long ExpiresUnixSeconds,
    ImmutableArray<string> AuthorizationReferences,
    int FileCount
)
{
    internal byte[] Encode() =>
        new DerSequence(
            DerInteger.ValueOf(2),
            DerInteger.ValueOf(1),
            new DerUtf8String(OrganizationId),
            new DerUtf8String(TransferId),
            new DerUtf8String(SenderId),
            new DerUtf8String(RecipientId),
            new DerUtf8String(EnrollmentId),
            DerInteger.ValueOf(Revision),
            new DerOctetString(SenderKeyId),
            new DerOctetString(RecipientKeyId),
            DerInteger.ValueOf((int)Purpose),
            DerInteger.ValueOf(CreatedUnixSeconds),
            DerInteger.ValueOf(ExpiresUnixSeconds),
            new DerSequence(
                AuthorizationReferences
                    .Select(value => (Asn1Encodable)new DerUtf8String(value))
                    .ToArray()
            ),
            DerInteger.ValueOf(FileCount)
        ).GetEncoded(Asn1Encodable.Der);

    internal static EnrollmentEnvelopeHeader Parse(Asn1Sequence sequence)
    {
        if (sequence.Count != 15 || !Number(sequence[0], 2) || !Number(sequence[1], 1))
            throw new ArgumentException("The envelope version or cipher suite is unsupported.");
        if (
            !EnrollmentDerIntegers.TryUInt(sequence[7], out var revision)
            || !EnrollmentDerIntegers.TryBoundedInt(sequence[10], 1, 4, out var purposeValue)
            || !EnrollmentDerIntegers.TryPositiveLong(sequence[11], out var created)
            || !EnrollmentDerIntegers.TryPositiveLong(sequence[12], out var expires)
            || !EnrollmentDerIntegers.TryBoundedInt(sequence[14], 1, 64, out var fileCount)
        )
            throw new ArgumentException("The revision is outside supported limits.");
        var purpose = (EnrollmentTransferPurpose)purposeValue;
        var result = new EnrollmentEnvelopeHeader(
            DerUtf8String.GetInstance(sequence[2]).GetString(),
            DerUtf8String.GetInstance(sequence[3]).GetString(),
            DerUtf8String.GetInstance(sequence[4]).GetString(),
            DerUtf8String.GetInstance(sequence[5]).GetString(),
            DerUtf8String.GetInstance(sequence[6]).GetString(),
            revision,
            Asn1OctetString.GetInstance(sequence[8]).GetOctets(),
            Asn1OctetString.GetInstance(sequence[9]).GetOctets(),
            purpose,
            created,
            expires,
            [
                .. Asn1Sequence
                    .GetInstance(sequence[13])
                    .Select(item => DerUtf8String.GetInstance(item).GetString()),
            ],
            fileCount
        );
        if (!Valid(result))
            throw new ArgumentException("Envelope header fields are invalid.");
        return result;
    }

    private static bool Valid(EnrollmentEnvelopeHeader h) =>
        OpaqueValid(h.OrganizationId)
        && OpaqueValid(h.TransferId)
        && OpaqueValid(h.SenderId)
        && OpaqueValid(h.RecipientId)
        && OpaqueValid(h.EnrollmentId)
        && h.SenderKeyId.Length == 32
        && h.RecipientKeyId.Length == 32
        && Enum.IsDefined(h.Purpose)
        && h.CreatedUnixSeconds > 0
        && h.ExpiresUnixSeconds <= 253_402_300_799
        && h.ExpiresUnixSeconds > h.CreatedUnixSeconds
        && h.ExpiresUnixSeconds - h.CreatedUnixSeconds <= 31_536_000
        && h.AuthorizationReferences.Length <= 64
        && h.AuthorizationReferences.All(ReferenceValid)
        && h.AuthorizationReferences.Distinct(StringComparer.Ordinal).Count()
            == h.AuthorizationReferences.Length
        && h.FileCount is >= 1 and <= 64;

    private static bool Number(Asn1Encodable value, int expected) =>
        EnrollmentDerIntegers.Equals(value, expected);

    private static bool TextValid(string value, int max) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= max
        && value.All(c => !char.IsControl(c));

    private static bool ReferenceValid(string value) =>
        TextValid(value, 512) && value.All(c => c is >= (char)0x21 and <= (char)0x7e);

    private static bool OpaqueValid(string value) =>
        TextValid(value, 256)
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
