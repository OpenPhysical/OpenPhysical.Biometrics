namespace OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Encodes the eight-byte UTC timestamp used by the PIV CBEFF patron format.</summary>
/// <remarks>Each pair of decimal digits is stored as its integer value. For example, century 20 is byte 0x14.</remarks>
public static class CbeffTimestampCodec
{
    /// <summary>Encodes an instant in UTC with whole-second precision.</summary>
    /// <param name="value">The instant to encode; its offset is converted to UTC.</param>
    /// <returns>A new caller-owned eight-byte buffer ending in the ASCII UTC indicator Z.</returns>
    public static byte[] Encode(DateTimeOffset value)
    {
        var result = new byte[8];
        Encode(value, result);
        return result;
    }

    internal static void Encode(DateTimeOffset value, Span<byte> destination)
    {
        var utc = value.ToUniversalTime();
        destination[0] = (byte)(utc.Year / 100);
        destination[1] = (byte)(utc.Year % 100);
        destination[2] = (byte)utc.Month;
        destination[3] = (byte)utc.Day;
        destination[4] = (byte)utc.Hour;
        destination[5] = (byte)utc.Minute;
        destination[6] = (byte)utc.Second;
        destination[7] = (byte)'Z';
    }

    /// <summary>Decodes decimal integer pairs and the UTC indicator into an immutable instant.</summary>
    /// <param name="value">Exactly eight encoded timestamp bytes, borrowed for this call.</param>
    /// <returns>The decoded instant with zero UTC offset.</returns>
    /// <exception cref="ArgumentException">The length, UTC indicator, or calendar fields are invalid.</exception>
    public static DateTimeOffset Decode(ReadOnlySpan<byte> value)
    {
        if (value.Length != 8 || value[0] > 99 || value[1] > 99 || value[7] != (byte)'Z')
            throw new ArgumentException(
                "A CBEFF timestamp requires eight bytes containing decimal integer pairs and a UTC indicator.",
                nameof(value)
            );
        try
        {
            return new(
                value[0] * 100 + value[1],
                value[2],
                value[3],
                value[4],
                value[5],
                value[6],
                TimeSpan.Zero
            );
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                "A CBEFF timestamp contains an invalid calendar date or time.",
                nameof(value)
            );
        }
    }
}
