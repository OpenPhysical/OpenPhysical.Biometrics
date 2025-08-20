namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using CSharpFunctionalExtensions;
using System.Collections.Immutable;

/// <summary>
/// Abstract base class for biometric format handlers providing common functionality.
/// </summary>
/// <typeparam name="TDomainRecord">The domain record type this handler processes.</typeparam>
public abstract class BiometricFormatHandlerBase<TDomainRecord> : IBiometricFormatHandler<TDomainRecord>
    where TDomainRecord : class
{
    /// <inheritdoc />
    public abstract string FormatName { get; }

    /// <inheritdoc />
    public abstract byte PatronFormat { get; }

    /// <inheritdoc />
    public abstract ushort BdbFormatOwner { get; }

    /// <inheritdoc />
    public abstract ushort BdbFormatType { get; }

    /// <inheritdoc />
    public abstract Result<TDomainRecord> ParseBiometricData(ImmutableArray<byte> data);

    /// <inheritdoc />
    public abstract Result<ImmutableArray<byte>> SerializeBiometricData(TDomainRecord record);

    /// <inheritdoc />
    public virtual Result ValidateBiometricData(ImmutableArray<byte> data)
    {
        return ParseBiometricData(data)
            .Map(_ => Result.Success())
            .Bind(result => result);
    }

    /// <summary>
    /// Validates that the data has sufficient length for the format.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <param name="minimumLength">The minimum required length.</param>
    /// <param name="formatName">The format name for error messages.</param>
    /// <returns>Success if data is long enough, failure otherwise.</returns>
    protected static Result ValidateDataLength(ImmutableArray<byte> data, int minimumLength, string formatName)
    {
        if (data.Length < minimumLength)
        {
            return Result.Failure($"{formatName} data must be at least {minimumLength} bytes (got {data.Length})");
        }
        return Result.Success();
    }

    /// <summary>
    /// Validates that a magic number or identifier matches the expected value.
    /// </summary>
    /// <param name="actual">The actual value read from data.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="fieldName">The field name for error messages.</param>
    /// <returns>Success if values match, failure otherwise.</returns>
    protected static Result ValidateIdentifier(uint actual, uint expected, string fieldName)
    {
        if (actual != expected)
        {
            return Result.Failure($"Invalid {fieldName}: expected 0x{expected:X8}, got 0x{actual:X8}");
        }
        return Result.Success();
    }

    /// <summary>
    /// Validates that a magic number or identifier matches the expected value.
    /// </summary>
    /// <param name="actual">The actual value read from data.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="fieldName">The field name for error messages.</param>
    /// <returns>Success if values match, failure otherwise.</returns>
    protected static Result ValidateIdentifier(ushort actual, ushort expected, string fieldName)
    {
        if (actual != expected)
        {
            return Result.Failure($"Invalid {fieldName}: expected 0x{expected:X4}, got 0x{actual:X4}");
        }
        return Result.Success();
    }

    /// <summary>
    /// Validates that a magic number or identifier matches the expected value.
    /// </summary>
    /// <param name="actual">The actual value read from data.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="fieldName">The field name for error messages.</param>
    /// <returns>Success if values match, failure otherwise.</returns>
    protected static Result ValidateIdentifier(byte actual, byte expected, string fieldName)
    {
        if (actual != expected)
        {
            return Result.Failure($"Invalid {fieldName}: expected 0x{expected:X2}, got 0x{actual:X2}");
        }
        return Result.Success();
    }

    /// <summary>
    /// Validates that a version number is supported.
    /// </summary>
    /// <param name="version">The version to validate.</param>
    /// <param name="supportedVersions">The list of supported versions.</param>
    /// <param name="formatName">The format name for error messages.</param>
    /// <returns>Success if version is supported, failure otherwise.</returns>
    protected static Result ValidateVersion(byte version, IReadOnlyCollection<byte> supportedVersions, string formatName)
    {
        if (!supportedVersions.Contains(version))
        {
            var supportedList = string.Join(", ", supportedVersions.Select(v => $"0x{v:X2}"));
            return Result.Failure($"Unsupported {formatName} version: 0x{version:X2} (supported: {supportedList})");
        }
        return Result.Success();
    }

    /// <summary>
    /// Validates that a range of bytes contains only valid values.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <param name="offset">The starting offset.</param>
    /// <param name="length">The number of bytes to check.</param>
    /// <param name="expectedValue">The expected value for each byte.</param>
    /// <param name="fieldName">The field name for error messages.</param>
    /// <returns>Success if all bytes match expected value, failure otherwise.</returns>
    protected static Result ValidateReservedBytes(ImmutableArray<byte> data, int offset, int length, byte expectedValue, string fieldName)
    {
        for (var i = 0; i < length; i++)
        {
            if (data[offset + i] != expectedValue)
            {
                return Result.Failure($"Invalid {fieldName} at offset {offset + i}: expected 0x{expectedValue:X2}, got 0x{data[offset + i]:X2}");
            }
        }
        return Result.Success();
    }

    /// <summary>
    /// Creates a memory stream from immutable array data for parsing.
    /// </summary>
    /// <param name="data">The data to create a stream from.</param>
    /// <returns>A memory stream positioned at the beginning of the data.</returns>
    protected static MemoryStream CreateDataStream(ImmutableArray<byte> data)
    {
        return new MemoryStream(data.ToArray(), writable: false);
    }

    /// <summary>
    /// Gets the format description for this handler.
    /// </summary>
    /// <returns>A string describing the format specification.</returns>
    public virtual string GetFormatDescription()
    {
        return $"{FormatName} (Patron: 0x{PatronFormat:X2}, Owner: 0x{BdbFormatOwner:X4}, Type: 0x{BdbFormatType:X4})";
    }

    /// <summary>
    /// Returns a string representation of this format handler.
    /// </summary>
    public override string ToString() => GetFormatDescription();
}