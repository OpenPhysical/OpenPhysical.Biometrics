namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// Biometric record implementation for INCITS 378 fingerprint minutiae templates.
/// </summary>
public sealed record Incits378BiometricRecord : BiometricRecord
{
    /// <summary>
    /// The parsed INCITS 378 fingerprint record.
    /// </summary>
    public FingerprintRecord FingerprintRecord { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Incits378BiometricRecord"/> class.
    /// </summary>
    /// <param name="rawData">The raw biometric data bytes.</param>
    /// <param name="fingerprintRecord">The parsed fingerprint record.</param>
    public Incits378BiometricRecord(ImmutableArray<byte> rawData, FingerprintRecord fingerprintRecord)
        : base(rawData)
    {
        FingerprintRecord = fingerprintRecord;
    }
    
    /// <summary>
    /// Gets the format type identifier for this record.
    /// </summary>
    public override string FormatType => "INCITS 378 Minutiae";
    
    /// <summary>
    /// Gets the biometric modality for this record.
    /// </summary>
    public string Modality => "Fingerprint";
    
    /// <summary>
    /// Gets a summary of the record content.
    /// </summary>
    public string Summary
    {
        get
        {
            if (FingerprintRecord.IsEmptyTemplate)
                return "Empty fingerprint template (0 minutiae)";
                
            var (avgQuality, totalMinutiae, hasUnusable) = FingerprintRecord.GetQualitySummary();
            var fingerCount = FingerprintRecord.ViewRecords.Length;
            
            var summary = $"{fingerCount} finger(s), {totalMinutiae} total minutiae";
            if (avgQuality > 0)
                summary += $", avg quality {avgQuality:F1}";
            if (hasUnusable)
                summary += " (includes unusable fingers)";
                
            return summary;
        }
    }
    
    /// <summary>
    /// Gets detailed information about the record.
    /// </summary>
    public IReadOnlyDictionary<string, object> GetDetails()
    {
        var details = new Dictionary<string, object>
        {
            ["Format"] = "INCITS 378-2004 Fingerprint Minutiae",
            ["IsEmpty"] = FingerprintRecord.IsEmptyTemplate,
            ["TotalMinutiae"] = FingerprintRecord.TotalMinutiae,
            ["FingerCount"] = FingerprintRecord.ViewRecords.Length,
            ["RecordLength"] = FingerprintRecord.Header.RecordLength,
            ["Resolution"] = $"{FingerprintRecord.Header.HorizontalResolution}x{FingerprintRecord.Header.VerticalResolution} pix/cm",
            ["ProductOwner"] = $"0x{FingerprintRecord.Header.ProductOwner:X4}",
            ["ProductType"] = $"0x{FingerprintRecord.Header.ProductType:X4}"
        };
        
        // Add finger-specific details
        for (int i = 0; i < FingerprintRecord.ViewRecords.Length; i++)
        {
            var view = FingerprintRecord.ViewRecords[i];
            var fingerKey = $"Finger{i + 1}";
            
            details[$"{fingerKey}_Position"] = $"{view.FingerPosition} ({view.GetFingerPositionName()})";
            details[$"{fingerKey}_ViewNumber"] = view.ViewNumber;
            details[$"{fingerKey}_ImpressionType"] = $"{view.ImpressionType} ({view.GetImpressionTypeName()})";
            details[$"{fingerKey}_Quality"] = view.FingerQuality;
            details[$"{fingerKey}_MinutiaeCount"] = view.NumberOfMinutiae;
            
            if (view.NumberOfMinutiae > 0)
            {
                var avgQuality = view.Minutiae.Where(m => m.Quality > 0).DefaultIfEmpty().Average(m => m?.Quality ?? 0);
                details[$"{fingerKey}_AvgMinutiaQuality"] = $"{avgQuality:F1}";
                
                var typeDistribution = view.Minutiae
                    .GroupBy(m => m.MinutiaType)
                    .ToDictionary(g => g.Key, g => g.Count());
                    
                foreach (var kvp in typeDistribution)
                {
                    var typeName = kvp.Key switch
                    {
                        0 => "RidgeEndings",
                        1 => "Bifurcations", 
                        _ => $"Type{kvp.Key}"
                    };
                    details[$"{fingerKey}_{typeName}"] = kvp.Value;
                }
            }
        }
        
        return details;
    }
}