// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Cbeff;

using CSharpFunctionalExtensions;

/// <summary>Encodes the eight-byte UTC timestamp used by the PIV CBEFF patron format.</summary>
/// <remarks>Each pair of decimal digits is stored as its integer value. For example, century 20 is byte 0x14.</remarks>
public static class CbeffTimestampCodec
{
    /// <summary>The stable error code for a malformed timestamp.</summary>
    public const string InvalidTimestampCode = "CbeffTimestamp";

    /// <summary>Encodes an instant in UTC with whole-second precision.</summary>
    /// <param name="value">The instant to encode; its offset is converted to UTC.</param>
    /// <returns>A new caller-owned eight-byte buffer ending in the ASCII UTC indicator Z.</returns>
    public static byte[] Encode(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return
        [
            (byte)(utc.Year / 100),
            (byte)(utc.Year % 100),
            (byte)utc.Month,
            (byte)utc.Day,
            (byte)utc.Hour,
            (byte)utc.Minute,
            (byte)utc.Second,
            (byte)'Z',
        ];
    }

    /// <summary>Decodes decimal integer pairs and the UTC indicator into an immutable instant.</summary>
    /// <param name="value">Exactly eight encoded timestamp bytes, borrowed for this call.</param>
    /// <returns>The decoded instant with zero UTC offset, or an error when the length, UTC indicator, or calendar fields are invalid.</returns>
    public static Result<DateTimeOffset, FormatError> Decode(ReadOnlySpan<byte> value) =>
        value.Length != 8 || value[0] > 99 || value[1] > 99 || value[7] != (byte)'Z'
            ? Result.Failure<DateTimeOffset, FormatError>(
                FormatError.Invalid(
                    InvalidTimestampCode,
                    "A CBEFF timestamp requires eight bytes containing decimal integer pairs and a UTC indicator."
                )
            )
            : FromFields(
                value[0] * 100 + value[1],
                value[2],
                value[3],
                value[4],
                value[5],
                value[6]
            );

    private static Result<DateTimeOffset, FormatError> FromFields(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second
    ) =>
        IsCalendarTime(year, month, day, hour, minute, second)
            ? new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero)
            : Result.Failure<DateTimeOffset, FormatError>(
                FormatError.Invalid(
                    InvalidTimestampCode,
                    "A CBEFF timestamp contains an invalid calendar date or time."
                )
            );

    private static bool IsCalendarTime(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second
    ) =>
        year is >= 1 and <= 9999
        && month is >= 1 and <= 12
        && day >= 1
        && day <= DateTime.DaysInMonth(year, month)
        && hour <= 23
        && minute <= 59
        && second <= 59;
}
