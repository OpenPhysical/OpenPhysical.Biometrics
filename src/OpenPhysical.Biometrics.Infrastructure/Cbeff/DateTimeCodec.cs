namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using Canonical = OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Provides DateTime compatibility over the canonical PIV CBEFF timestamp codec.</summary>
public static class DateTimeCodec
{
    /// <summary>Converts a DateTime to UTC and encodes decimal integer pairs with second precision.</summary>
    /// <param name="dateTime">The timestamp, interpreted according to its DateTime kind.</param>
    /// <returns>A new caller-owned eight-byte buffer.</returns>
    public static byte[] Encode(DateTime dateTime) =>
        Canonical.CbeffTimestampCodec.Encode(new DateTimeOffset(dateTime.ToUniversalTime()));

    /// <summary>Returns encoded timestamp bytes for enumerable-based compatibility callers.</summary>
    /// <param name="dateTime">The timestamp, interpreted according to its DateTime kind.</param>
    /// <returns>A separate eight-byte encoded sequence.</returns>
    public static IEnumerable<byte> EncodeAsEnumerable(DateTime dateTime) => Encode(dateTime);

    /// <summary>Decodes an eight-byte PIV timestamp to a UTC DateTime.</summary>
    /// <param name="encodedBytes">The complete timestamp bytes.</param>
    /// <returns>The UTC timestamp or a structural failure.</returns>
    public static Result<DateTime> Decode(byte[] encodedBytes)
    {
        if (encodedBytes is null)
            return Result.Failure<DateTime>("CBEFF.TimestampMissing");
        try
        {
            return Result.Success(Canonical.CbeffTimestampCodec.Decode(encodedBytes).UtcDateTime);
        }
        catch (ArgumentException)
        {
            return Result.Failure<DateTime>("CBEFF.TimestampRepresentation");
        }
    }

    /// <summary>Materializes an encoded sequence once and decodes its complete timestamp.</summary>
    /// <param name="encodedBytes">The complete encoded timestamp sequence.</param>
    /// <returns>The UTC timestamp or a structural failure.</returns>
    public static Result<DateTime> DecodeFromEnumerable(IEnumerable<byte> encodedBytes) =>
        encodedBytes is null
            ? Result.Failure<DateTime>("CBEFF.TimestampMissing")
            : Decode(encodedBytes.Take(9).ToArray());

    /// <summary>Decodes the compatibility sequence and throws when its timestamp is invalid.</summary>
    /// <param name="encodedBytes">The encoded timestamp sequence.</param>
    /// <returns>The UTC timestamp.</returns>
    /// <exception cref="ArgumentException">The sequence is null or malformed.</exception>
    public static DateTime DecodeUnsafe(IEnumerable<byte> encodedBytes)
    {
        var result = DecodeFromEnumerable(encodedBytes);
        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error, nameof(encodedBytes));
    }

    /// <summary>Checks the complete timestamp, including calendar validity.</summary>
    /// <param name="encodedBytes">The encoded timestamp bytes.</param>
    /// <returns>Whether the canonical timestamp decoder accepts the input.</returns>
    public static bool IsValidFormat(byte[] encodedBytes) => Decode(encodedBytes).IsSuccess;
}
