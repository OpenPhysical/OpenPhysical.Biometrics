namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSharpFunctionalExtensions;

/// <summary>
/// Codec for encoding and decoding DateTime values in CBEFF format per NIST SP 800-76-2.
/// For PIV, each pair of digits is stored as an unsigned integer byte (NOT BCD).
/// Example: "20" is stored as 20 (0x14), not as BCD 0x20.
/// </summary>
public static class DateTimeCodec
{
    /// <summary>
    /// Encodes a DateTime into CBEFF format per NIST SP 800-76-2 (8 bytes: YYYYMMDDhhmmssZ).
    /// Each pair of digits is stored as its decimal value in one byte.
    /// Example: 17:35:30 December 15, 2005 → [20, 5, 12, 15, 17, 35, 30, 90]
    /// </summary>
    /// <param name="dateTime">The DateTime to encode</param>
    /// <returns>8 bytes in NIST PIV format with UTC indicator</returns>
    public static byte[] Encode(DateTime dateTime)
    {
        // Convert to UTC if not already
        var utcDate = dateTime.ToUniversalTime();
        
        var result = new byte[8];
        
        // Split year into century and year within century
        result[0] = (byte)(utcDate.Year / 100);    // Century (e.g., 20 for 2005)
        result[1] = (byte)(utcDate.Year % 100);    // Year (e.g., 5 for 2005)
        result[2] = (byte)utcDate.Month;           // Month (1-12)
        result[3] = (byte)utcDate.Day;             // Day (1-31)
        result[4] = (byte)utcDate.Hour;            // Hour (0-23)
        result[5] = (byte)utcDate.Minute;          // Minute (0-59)
        result[6] = (byte)utcDate.Second;          // Second (0-59)
        result[7] = (byte)'Z';                     // UTC indicator (ASCII 90)
        
        return result;
    }

    /// <summary>
    /// Encodes a DateTime into CBEFF format as an enumerable per NIST SP 800-76-2.
    /// </summary>
    /// <param name="dateTime">The DateTime to encode</param>
    /// <returns>Enumerable of 8 bytes in NIST PIV format</returns>
    public static IEnumerable<byte> EncodeAsEnumerable(DateTime dateTime)
    {
        var encoded = Encode(dateTime);
        foreach (var b in encoded)
        {
            yield return b;
        }
    }

    /// <summary>
    /// Decodes CBEFF format bytes into a DateTime object per NIST SP 800-76-2.
    /// </summary>
    /// <param name="encodedBytes">The encoded bytes (must be 8 bytes)</param>
    /// <returns>Result containing the decoded DateTime or error</returns>
    public static Result<DateTime> Decode(byte[] encodedBytes)
    {
        if (encodedBytes == null)
            return Result.Failure<DateTime>("Encoded bytes cannot be null");
            
        if (encodedBytes.Length != 8)
            return Result.Failure<DateTime>($"Expected 8 bytes, got {encodedBytes.Length}");

        return DecodeFromEnumerable(encodedBytes);
    }

    /// <summary>
    /// Decodes CBEFF format bytes from an enumerable into a DateTime object per NIST SP 800-76-2.
    /// </summary>
    /// <param name="encodedBytes">The encoded bytes</param>
    /// <returns>Result containing the decoded DateTime or error</returns>
    public static Result<DateTime> DecodeFromEnumerable(IEnumerable<byte> encodedBytes)
    {
        if (encodedBytes == null)
            return Result.Failure<DateTime>("Encoded bytes cannot be null");

        var byteArray = encodedBytes.ToArray();
        if (byteArray.Length != 8)
            return Result.Failure<DateTime>($"Expected 8 bytes, got {byteArray.Length}");

        try
        {
            // Check that the last byte is 'Z' (90 in ASCII)
            if (byteArray[7] != (byte)'Z')
            {
                return Result.Failure<DateTime>($"Expected UTC indicator 'Z' (90), got {byteArray[7]}");
            }

            // Decode the date components
            var year = byteArray[0] * 100 + byteArray[1];  // Century + year
            var month = byteArray[2];
            var day = byteArray[3];
            var hour = byteArray[4];
            var minute = byteArray[5];
            var second = byteArray[6];

            // Validate ranges
            if (month < 1 || month > 12)
                return Result.Failure<DateTime>($"Invalid month: {month}");
            if (day < 1 || day > 31)
                return Result.Failure<DateTime>($"Invalid day: {day}");
            if (hour > 23)
                return Result.Failure<DateTime>($"Invalid hour: {hour}");
            if (minute > 59)
                return Result.Failure<DateTime>($"Invalid minute: {minute}");
            if (second > 59)
                return Result.Failure<DateTime>($"Invalid second: {second}");

            return Result.Success(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
        }
        catch (Exception ex)
        {
            return Result.Failure<DateTime>($"Error decoding date: {ex.Message}");
        }
    }

    /// <summary>
    /// Unsafe decode method for compatibility with existing code.
    /// </summary>
    /// <param name="encodedBytes">The encoded bytes</param>
    /// <returns>Decoded DateTime</returns>
    /// <exception cref="ArgumentException">If the format is invalid</exception>
    public static DateTime DecodeUnsafe(IEnumerable<byte> encodedBytes)
    {
        var result = DecodeFromEnumerable(encodedBytes);
        if (result.IsFailure)
            throw new ArgumentException(result.Error);
        return result.Value;
    }

    /// <summary>
    /// Validates that the given bytes are in valid CBEFF format per NIST SP 800-76-2.
    /// </summary>
    /// <param name="encodedBytes">The bytes to validate</param>
    /// <returns>True if valid format, false otherwise</returns>
    public static bool IsValidFormat(byte[] encodedBytes)
    {
        if (encodedBytes == null || encodedBytes.Length != 8)
            return false;

        // Last byte must be 'Z' (90 in ASCII)
        if (encodedBytes[7] != (byte)'Z')
            return false;

        // Validate date components
        var month = encodedBytes[2];
        var day = encodedBytes[3];
        var hour = encodedBytes[4];
        var minute = encodedBytes[5];
        var second = encodedBytes[6];

        return month >= 1 && month <= 12 &&
               day >= 1 && day <= 31 &&
               hour <= 23 &&
               minute <= 59 &&
               second <= 59;
    }
}