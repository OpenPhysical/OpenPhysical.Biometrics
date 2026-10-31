namespace OpenPhysical.Biometrics.Core.Services;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Domain;

/// <summary>
/// Provides functional encoding and decoding operations for FASC-N (Federal Agency Smart Credential Number).
/// </summary>
public static class FascNCodec
{
    private const int FieldSeparator = -1;
    private const int StartSentinel = -2;
    private const int EndSentinel = -3;
    private const int UnknownInput = -4;

    private const int PositionStartSentinel = 0;
    private const int PositionEndSentinel = 38;
    private const int PositionLrc = 39;
    private const int PositionAgencyStart = 1;
    private const int PositionSystemStart = 6;
    private const int PositionCredentialNumber = 11;
    private const int PositionCredentialSeries = 18;
    private const int PositionCredentialIssue = 20;
    private const int PositionPersonIdentifier = 22;
    private const int PositionOrganizationalCategory = 32;
    private const int PositionOrganizationalIdentifier = 33;
    private const int PositionPersonOrganizationalAffiliation = 37;

    private const int BinaryFascnSize = 200;
    private const int HexFascnSize = 50;
    private const int BcdChunkSize = 5;

    private static readonly int[] FsPositions =
    {
        PositionSystemStart - 1,
        PositionCredentialNumber - 1,
        PositionCredentialSeries - 1,
        PositionCredentialIssue - 1,
        PositionPersonIdentifier - 1,
    };

    private static readonly ImmutableDictionary<int, string> BcdFourBitParity = new Dictionary<
        int,
        string
    >
    {
        { StartSentinel, "11010" },
        { EndSentinel, "11111" },
        { FieldSeparator, "10110" },
        { 0, "00001" },
        { 1, "10000" },
        { 2, "01000" },
        { 3, "11001" },
        { 4, "00100" },
        { 5, "10101" },
        { 6, "01101" },
        { 7, "11100" },
        { 8, "00010" },
        { 9, "10011" },
    }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<string, int> FourBitParityBcd =
        BcdFourBitParity.ToImmutableDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>
    /// Decodes a FASC-N from byte array.
    /// </summary>
    public static Result<FascN> Decode(byte[] fascnBytes)
    {
        if (fascnBytes == null || fascnBytes.Length == 0)
            return Result.Failure<FascN>("FASC-N bytes cannot be null or empty");

        var hexString = Convert.ToHexString(fascnBytes);
        return Decode(hexString);
    }

    /// <summary>
    /// Decodes a FASC-N from hex or binary string.
    /// </summary>
    public static Result<FascN> Decode(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return Result.Failure<FascN>("FASC-N identifier cannot be null or empty");

        var cleaned = identifier.Replace(" ", "");

        return ValidateAndConvertToBinary(cleaned)
            .Bind(binary =>
                ParseBinarySegments(binary)
                    .Map(segments => (segments, SplitIntoBcdSegments(binary)))
            )
            .Bind(tuple => ValidateSegments(tuple.segments, tuple.Item2))
            .Bind(ExtractFascNData);
    }

    /// <summary>
    /// Encodes a FASC-N to hexadecimal string.
    /// </summary>
    public static Result<string> Encode(FascN fascn)
    {
        return Result
            .Success(fascn)
            .Map(CreateSegments)
            .Map(ConvertToBinary)
            .Map(CalculateAndAppendLrc)
            .Map(ConvertToHex);
    }

    private static Result<string> ValidateAndConvertToBinary(string cleaned)
    {
        return cleaned.Length switch
        {
            BinaryFascnSize when !Regex.IsMatch(cleaned, "^[01]+$") => Result.Failure<string>(
                "Binary string must contain only 0 and 1"
            ),
            BinaryFascnSize => Result.Success(cleaned),
            HexFascnSize when !Regex.IsMatch(cleaned, "^[0-9a-fA-F]+$") => Result.Failure<string>(
                "Hex string must contain only hexadecimal characters"
            ),
            HexFascnSize => Result.Success(HexToBinary(cleaned)),
            _ => Result.Failure<string>(
                $"Input must be either {HexFascnSize} hex chars or {BinaryFascnSize} binary chars"
            ),
        };
    }

    private static Result<int[]> ParseBinarySegments(string binaryString)
    {
        try
        {
            var segments = SplitIntoBcdSegments(binaryString);
            var result = new int[segments.Length];

            for (int i = 0; i < segments.Length; i++)
            {
                if (i != PositionLrc)
                {
                    if (!FourBitParityBcd.TryGetValue(segments[i], out result[i]))
                    {
                        return Result.Failure<int[]>(
                            $"Unknown BCD segment at position {i}: {segments[i]}"
                        );
                    }
                }
                else
                {
                    result[i] = Convert.ToInt32(segments[i], 2);
                }
            }

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<int[]>($"Failed to parse binary segments: {ex.Message}");
        }
    }

    private static Result<int[]> ValidateSegments(int[] segments, string[] bcdSegments)
    {
        return Result
            .Success(segments)
            .Ensure(_ => segments[PositionStartSentinel] == StartSentinel, "Missing start sentinel")
            .Ensure(_ => segments[PositionEndSentinel] == EndSentinel, "Missing end sentinel")
            .Ensure(
                _ => FsPositions.All(pos => segments[pos] == FieldSeparator),
                "Missing field separators"
            )
            .Bind(segs => ValidateLrc(segs, bcdSegments));
    }

    private static Result<int[]> ValidateLrc(int[] segments, string[] bcdSegments)
    {
        try
        {
            // Use the LRC value directly from segments array (already parsed from binary)
            int lrc = segments[PositionLrc] & 0b11110;

            // XOR with all segments except the LRC itself
            for (int i = 0; i < bcdSegments.Length - 1; i++)
            {
                lrc ^= Convert.ToInt32(bcdSegments[i], 2) & 0b11110;
            }

            // Count the number of 1 bits in the LRC segment
            var lrcParity = bcdSegments[PositionLrc].Count(c => c == '1');

            return Result
                .Success(segments)
                .Ensure(_ => lrcParity % 2 == 1, "Invalid LRC parity")
                .Ensure(_ => lrc == 0, "Invalid LRC checksum");
        }
        catch (Exception ex)
        {
            return Result.Failure<int[]>($"LRC validation failed: {ex.Message}");
        }
    }

    private static Result<FascN> ExtractFascNData(int[] segments)
    {
        try
        {
            var agencyCode = CoalesceIntegerArray(
                segments,
                PositionAgencyStart,
                PositionSystemStart - 1
            );
            var systemCode = CoalesceIntegerArray(
                segments,
                PositionSystemStart,
                PositionCredentialNumber - 1
            );
            var credentialNumber = CoalesceIntegerArray(
                segments,
                PositionCredentialNumber,
                PositionCredentialSeries - 1
            );
            var credentialSeries = CoalesceIntegerArray(
                segments,
                PositionCredentialSeries,
                PositionCredentialIssue - 1
            );
            var credentialIssue = CoalesceIntegerArray(
                segments,
                PositionCredentialIssue,
                PositionPersonIdentifier - 1
            );
            var personIdentifier = CoalesceIntegerArrayLong(
                segments,
                PositionPersonIdentifier,
                PositionOrganizationalCategory
            );
            var organizationalCategory = CoalesceIntegerArray(
                segments,
                PositionOrganizationalCategory,
                PositionOrganizationalIdentifier
            );
            var organizationalIdentifier = CoalesceIntegerArray(
                segments,
                PositionOrganizationalIdentifier,
                PositionPersonOrganizationalAffiliation
            );
            var personOrganizationAffiliation = CoalesceIntegerArray(
                segments,
                PositionPersonOrganizationalAffiliation,
                PositionEndSentinel
            );

            return FascN.Create(
                agencyCode,
                systemCode,
                credentialNumber,
                credentialSeries,
                credentialIssue,
                personIdentifier,
                (FascN.OrganizationalCategory)organizationalCategory,
                organizationalIdentifier,
                (FascN.PersonOrganizationAffiliation)personOrganizationAffiliation
            );
        }
        catch (Exception ex)
        {
            return Result.Failure<FascN>($"Failed to extract FASC-N data: {ex.Message}");
        }
    }

    private static int[] CreateSegments(FascN fascn)
    {
        var segments = new int[BinaryFascnSize / BcdChunkSize - 1];

        segments[PositionStartSentinel] = StartSentinel;
        segments[PositionEndSentinel] = EndSentinel;

        foreach (var position in FsPositions)
            segments[position] = FieldSeparator;

        SetDigits(segments, PositionAgencyStart, fascn.AgencyCode, 4);
        SetDigits(segments, PositionSystemStart, fascn.SystemCode, 4);
        SetDigits(segments, PositionCredentialNumber, fascn.CredentialNumber, 6);
        SetDigits(segments, PositionCredentialSeries, fascn.CredentialSeries, 1);
        SetDigits(segments, PositionCredentialIssue, fascn.IndividualCredentialIssue, 1);
        SetDigits(segments, PositionPersonIdentifier, fascn.PersonIdentifier, 10);
        SetDigits(segments, PositionOrganizationalCategory, (int)fascn.Category, 1);
        SetDigits(segments, PositionOrganizationalIdentifier, fascn.OrganizationalIdentifier, 4);
        SetDigits(segments, PositionPersonOrganizationalAffiliation, (int)fascn.Affiliation, 1);

        return segments;
    }

    private static string ConvertToBinary(int[] segments)
    {
        return string.Concat(segments.Select(s => BcdFourBitParity[s]));
    }

    private static string CalculateAndAppendLrc(string binary)
    {
        var lrc = new int[5];

        for (int i = 0; i < binary.Length / 5; i++)
        {
            var lrcBlock = binary
                .Substring(i * 5, 5)
                .Select(c => int.Parse(c.ToString()))
                .ToArray();
            for (int j = 0; j < lrcBlock.Length; j++)
                lrc[j] ^= lrcBlock[j];
        }

        lrc[4] = lrc.Take(4).Sum() % 2 == 0 ? 1 : 0;

        return binary + string.Concat(lrc.Select(l => l.ToString()));
    }

    private static string ConvertToHex(string binary)
    {
        var nBytes = binary.Length / 8;
        var bytes = Enumerable
            .Range(0, nBytes)
            .Select(i => Convert.ToByte(binary.Substring(8 * i, 8), 2))
            .ToArray();

        return Convert.ToHexString(bytes);
    }

    private static string HexToBinary(string hex)
    {
        return string.Concat(
            hex.Select(c => Convert.ToString(Convert.ToInt32(c.ToString(), 16), 2).PadLeft(4, '0'))
        );
    }

    private static string[] SplitIntoBcdSegments(string binary)
    {
        var segmentCount = binary.Length / BcdChunkSize;
        return Enumerable
            .Range(0, segmentCount)
            .Select(i => binary.Substring(i * BcdChunkSize, BcdChunkSize))
            .ToArray();
    }

    private static int CoalesceIntegerArray(int[] segments, int start, int end)
    {
        return segments[start..end].Aggregate(0, (acc, digit) => acc * 10 + digit);
    }

    private static long CoalesceIntegerArrayLong(int[] segments, int start, int end)
    {
        return segments[start..end].Aggregate(0L, (acc, digit) => acc * 10 + digit);
    }

    private static void SetDigits(int[] segments, int position, long value, int digits)
    {
        var digitArray = value
            .ToString($"D{digits}")
            .Select(c => int.Parse(c.ToString()))
            .ToArray();
        Array.Copy(digitArray, 0, segments, position, digitArray.Length);
    }
}
