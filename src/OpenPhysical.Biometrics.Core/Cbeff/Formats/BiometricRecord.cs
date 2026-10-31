namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using System.Collections.Immutable;

/// <summary>
/// Base class for all biometric records parsed from CBEFF data blocks.
/// </summary>
public abstract record BiometricRecord
{
    /// <summary>
    /// Gets the format type identifier for this record.
    /// </summary>
    public abstract string FormatType { get; }

    /// <summary>
    /// Gets the raw bytes of the biometric data.
    /// </summary>
    public ImmutableArray<byte> RawData { get; }

    /// <summary>
    /// Initializes a new instance of the BiometricRecord class.
    /// </summary>
    /// <param name="rawData">The raw biometric data bytes.</param>
    protected BiometricRecord(ImmutableArray<byte> rawData)
    {
        RawData = rawData;
    }
}
