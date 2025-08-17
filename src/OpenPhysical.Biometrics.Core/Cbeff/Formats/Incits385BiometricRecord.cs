namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using System.Collections.Immutable;

/// <summary>
/// Represents a parsed INCITS 385 facial image biometric record.
/// </summary>
public sealed record Incits385BiometricRecord : BiometricRecord
{
    /// <summary>
    /// Gets the format type identifier.
    /// </summary>
    public override string FormatType => "INCITS 385";
    
    /// <summary>
    /// Gets the parsed facial image record.
    /// </summary>
    public FacialImageRecord FacialRecord { get; }
    
    /// <summary>
    /// Initializes a new instance of the Incits385BiometricRecord class.
    /// </summary>
    /// <param name="rawData">The raw biometric data bytes.</param>
    /// <param name="facialRecord">The parsed facial image record.</param>
    public Incits385BiometricRecord(ImmutableArray<byte> rawData, FacialImageRecord facialRecord) 
        : base(rawData)
    {
        FacialRecord = facialRecord ?? throw new ArgumentNullException(nameof(facialRecord));
    }
}