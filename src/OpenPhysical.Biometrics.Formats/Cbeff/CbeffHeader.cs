using System.Buffers.Binary;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Preserves the exact 88-byte PIV CBEFF header and exposes its typed fields.</summary>
public sealed class CbeffHeader
{
    /// <summary>Gets the fixed PIV patron-header size in bytes.</summary>
    public const int Size = 88;
    private readonly ImmutableArray<byte> bytes;

    /// <summary>Creates a header from a complete 88-byte buffer, taking a defensive copy.</summary>
    /// <param name="rawHeader">Header bytes in their signed wire representation.</param>
    /// <exception cref="ArgumentException">The length or timestamp representation is invalid.</exception>
    public CbeffHeader(ReadOnlySpan<byte> rawHeader)
    {
        if (rawHeader.Length != Size)
            throw new ArgumentException(
                "A PIV CBEFF header contains exactly 88 bytes.",
                nameof(rawHeader)
            );
        _ = DecodeDate(rawHeader.Slice(12, 8));
        _ = DecodeDate(rawHeader.Slice(20, 8));
        _ = DecodeDate(rawHeader.Slice(28, 8));
        bytes = ImmutableArray.Create(rawHeader.ToArray());
    }

    /// <summary>Gets the original, immutable signed header bytes.</summary>
    public ImmutableArray<byte> RawBytes => bytes;

    /// <summary>Gets the patron header version.</summary>
    public byte PatronHeaderVersion => bytes[0];

    /// <summary>Gets the security-options byte.</summary>
    public byte SecurityOptions => bytes[1];

    /// <summary>Gets the declared biometric data length.</summary>
    public uint BiometricDataLength => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(2, 4));

    /// <summary>Gets the declared detached security-block length.</summary>
    public ushort SignatureLength => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(6, 2));

    /// <summary>Gets the registered biometric format owner.</summary>
    public ushort FormatOwner => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2));

    /// <summary>Gets the registered biometric format type.</summary>
    public ushort FormatType => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(10, 2));

    /// <summary>Gets the creation timestamp in UTC with second precision.</summary>
    public DateTimeOffset CreationDate => DecodeDate(bytes.AsSpan(12, 8));

    /// <summary>Gets the start of the biometric validity interval.</summary>
    public DateTimeOffset ValidityNotBefore => DecodeDate(bytes.AsSpan(20, 8));

    /// <summary>Gets the end of the biometric validity interval.</summary>
    public DateTimeOffset ValidityNotAfter => DecodeDate(bytes.AsSpan(28, 8));

    /// <summary>Gets the 24-bit biometric type mask.</summary>
    public uint BiometricType => (uint)(bytes[36] << 16 | bytes[37] << 8 | bytes[38]);

    /// <summary>Gets the encoded biometric processing category and data-type flags.</summary>
    public byte BiometricDataType => bytes[39];

    /// <summary>Gets the signed biometric quality value.</summary>
    public sbyte Quality => unchecked((sbyte)bytes[40]);

    /// <summary>Gets the 18-byte creator field.</summary>
    public ImmutableArray<byte> Creator => ImmutableArray.Create(bytes.AsSpan(41, 18).ToArray());

    /// <summary>Gets the 25-byte FASC-N identity field.</summary>
    public ImmutableArray<byte> FascN => ImmutableArray.Create(bytes.AsSpan(59, 25).ToArray());

    /// <summary>Gets the four reserved bytes.</summary>
    public ImmutableArray<byte> Reserved => ImmutableArray.Create(bytes.AsSpan(84, 4).ToArray());

    /// <summary>Returns a separate mutable copy of the exact header.</summary>
    /// <returns>Header bytes owned by the caller.</returns>
    public byte[] ToArray() => bytes.ToArray();

    /// <summary>Returns a copy with the security-block length updated before signing.</summary>
    /// <param name="length">Detached signature length in bytes.</param>
    /// <returns>A new immutable header.</returns>
    public CbeffHeader WithSignatureLength(ushort length)
    {
        var copy = ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(6, 2), length);
        return new(copy);
    }

    /// <summary>Returns a copy with the biometric data length updated.</summary>
    /// <param name="length">Biometric data length in bytes.</param>
    /// <returns>A new immutable header.</returns>
    public CbeffHeader WithBiometricDataLength(uint length)
    {
        var copy = ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(2, 4), length);
        return new(copy);
    }

    /// <summary>Creates a PIV patron header from explicit metadata.</summary>
    /// <param name="biometricDataLength">Encoded biometric data size.</param>
    /// <param name="signatureLength">Encoded detached security-block size.</param>
    /// <param name="formatOwner">Registered format owner.</param>
    /// <param name="formatType">Registered format type.</param>
    /// <param name="creationDate">Creation time, encoded in UTC at second precision.</param>
    /// <param name="validityNotBefore">Validity start, encoded in UTC at second precision.</param>
    /// <param name="validityNotAfter">Validity end, encoded in UTC at second precision.</param>
    /// <param name="biometricType">24-bit modality mask.</param>
    /// <param name="biometricDataType">Biometric processing category and data-type flags.</param>
    /// <param name="quality">Signed header quality value.</param>
    /// <param name="creator">Exactly 18 creator bytes.</param>
    /// <param name="fascN">Exactly 25 FASC-N bytes.</param>
    /// <param name="securityOptions">Security options specified by the selected PIV profile.</param>
    /// <param name="patronHeaderVersion">Patron version, normally three.</param>
    /// <returns>A new immutable header with zeroed reserved bytes.</returns>
    public static CbeffHeader Create(
        uint biometricDataLength,
        ushort signatureLength,
        ushort formatOwner,
        ushort formatType,
        DateTimeOffset creationDate,
        DateTimeOffset validityNotBefore,
        DateTimeOffset validityNotAfter,
        uint biometricType,
        byte biometricDataType,
        sbyte quality,
        ReadOnlySpan<byte> creator,
        ReadOnlySpan<byte> fascN,
        byte securityOptions = 0,
        byte patronHeaderVersion = 3
    )
    {
        if (creator.Length != 18)
            throw new ArgumentException(
                "The creator field contains exactly 18 bytes.",
                nameof(creator)
            );
        if (fascN.Length != 25)
            throw new ArgumentException(
                "The FASC-N field contains exactly 25 bytes.",
                nameof(fascN)
            );
        if (biometricType > 0xFFFFFF)
            throw new ArgumentOutOfRangeException(nameof(biometricType));
        var result = new byte[Size];
        result[0] = patronHeaderVersion;
        result[1] = securityOptions;
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(2), biometricDataLength);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(6), signatureLength);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(8), formatOwner);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(10), formatType);
        EncodeDate(creationDate, result.AsSpan(12, 8));
        EncodeDate(validityNotBefore, result.AsSpan(20, 8));
        EncodeDate(validityNotAfter, result.AsSpan(28, 8));
        result[36] = (byte)(biometricType >> 16);
        result[37] = (byte)(biometricType >> 8);
        result[38] = (byte)biometricType;
        result[39] = biometricDataType;
        result[40] = unchecked((byte)quality);
        creator.CopyTo(result.AsSpan(41));
        fascN.CopyTo(result.AsSpan(59));
        return new(result);
    }

    private static void EncodeDate(DateTimeOffset value, Span<byte> destination) =>
        CbeffTimestampCodec.Encode(value, destination);

    internal static DateTimeOffset DecodeDate(ReadOnlySpan<byte> value) =>
        CbeffTimestampCodec.Decode(value);
}
