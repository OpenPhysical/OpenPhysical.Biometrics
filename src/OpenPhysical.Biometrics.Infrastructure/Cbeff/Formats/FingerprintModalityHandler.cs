namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text;

/// <summary>
/// Modality handler for fingerprint biometric data processing and extraction.
/// </summary>
public sealed class FingerprintModalityHandler : IBiometricModalityHandler<FingerprintRecord, ExtractedBiometricData>
{
    /// <inheritdoc />
    public string ModalityName => "Fingerprint";
    
    /// <inheritdoc />
    public bool CanHandle(FingerprintRecord record)
    {
        return record != null;
    }
    
    /// <inheritdoc />
    public Result<ExtractedBiometricData> ExtractBiometricData(FingerprintRecord record)
    {
        var minutiaeData = CreateMinutiaeData(record);
        var json = JsonSerializer.Serialize(minutiaeData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var metadata = CreateMetadata(record);
        
        var fingerprintData = new FingerprintMinutiaeData(
            jsonBytes.ToImmutableArray(),
            "INCITS 378",
            record.TotalMinutiae,
            record.ViewRecords.Length,
            record.IsEmptyTemplate,
            metadata);
            
        return Result.Success<ExtractedBiometricData>(fingerprintData);
    }
    
    /// <inheritdoc />
    public Result<string> AnalyzeBiometricData(FingerprintRecord record)
    {
        var analysis = new
        {
            FormatIdentifier = Encoding.ASCII.GetString(record.Header.FormatIdentifier.ToArray()).TrimEnd('\0'),
            VersionNumber = Encoding.ASCII.GetString(record.Header.VersionNumber.ToArray()).TrimEnd('\0'),
            RecordLength = record.Header.RecordLength,
            NumberOfFingerRecords = record.Header.NumberOfFingerRecords,
            TotalMinutiae = record.TotalMinutiae,
            IsEmptyTemplate = record.IsEmptyTemplate,
            QualitySummary = record.GetQualitySummary(),
            ViewRecords = record.ViewRecords.Select(view => new
            {
                FingerPosition = GetFingerPositionName(view.FingerPosition),
                ViewNumber = view.ViewNumber,
                ImpressionType = GetImpressionTypeName(view.ImpressionType),
                FingerQuality = view.FingerQuality,
                NumberOfMinutiae = view.NumberOfMinutiae,
                MinutiaeQuality = view.Minutiae.Any() 
                    ? new { Average = view.Minutiae.Average(m => m.Quality), Min = view.Minutiae.Min(m => m.Quality), Max = view.Minutiae.Max(m => m.Quality) }
                    : null
            }).ToArray(),
            CaptureInfo = new
            {
                ProductOwner = $"0x{record.Header.ProductOwner:X4}",
                ProductType = $"0x{record.Header.ProductType:X4}",
                EquipmentCompliance = $"0x{record.Header.GetCaptureEquipmentComplianceValue():X}",
                EquipmentId = record.Header.GetCaptureEquipmentId(),
                ImageSize = $"{record.Header.ImageSizeX}×{record.Header.ImageSizeY}",
                Resolution = $"{record.Header.XResolution}×{record.Header.YResolution} pixels/cm"
            }
        };
        
        var json = JsonSerializer.Serialize(analysis, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        return Result.Success(json);
    }
    
    /// <inheritdoc />
    public Result ValidateModalityRequirements(FingerprintRecord record)
    {
        var errors = new List<string>();
        
        // Validate PIV requirements per NIST SP 800-76-2
        if (record.Header.ProductOwner != 0x0001 || record.Header.ProductType != 0x0001)
        {
            errors.Add($"Expected NIST Product Owner/Type (0x0001/0x0001), got (0x{record.Header.ProductOwner:X4}/0x{record.Header.ProductType:X4})");
        }
        
        // Validate equipment compliance (should be 0x8 for EFTS Appendix F compliance)
        var complianceBits = record.Header.GetCaptureEquipmentComplianceValue();
        if (complianceBits != 0x8)
        {
            errors.Add($"Expected equipment compliance 0x8 (EFTS Appendix F compliant), got 0x{complianceBits:X}");
        }
        
        // Validate resolution (PIV typically uses 500 DPI ≈ 197 pixels/cm)
        var expectedResolution = 197; // 500 DPI converted to pixels per cm
        if (Math.Abs(record.Header.XResolution - expectedResolution) > 5)
        {
            errors.Add($"X resolution {record.Header.XResolution} pixels/cm not close to 500 DPI (~{expectedResolution} pixels/cm)");
        }
        
        if (Math.Abs(record.Header.YResolution - expectedResolution) > 5)
        {
            errors.Add($"Y resolution {record.Header.YResolution} pixels/cm not close to 500 DPI (~{expectedResolution} pixels/cm)");
        }
        
        // Validate finger view records
        foreach (var viewRecord in record.ViewRecords)
        {
            if (viewRecord.FingerQuality == 0)
            {
                errors.Add($"Finger position {viewRecord.FingerPosition} has quality score 0 (failure)");
            }
            
            if (viewRecord.NumberOfMinutiae == 0 && !record.IsEmptyTemplate)
            {
                errors.Add($"Finger position {viewRecord.FingerPosition} reports 0 minutiae but template not marked as empty");
            }
            
            if (viewRecord.NumberOfMinutiae != viewRecord.Minutiae.Length)
            {
                errors.Add($"Finger position {viewRecord.FingerPosition} reports {viewRecord.NumberOfMinutiae} minutiae but contains {viewRecord.Minutiae.Length} minutiae records");
            }
        }
        
        return errors.Count == 0 
            ? Result.Success()
            : Result.Failure(string.Join("; ", errors));
    }
    
    private static object CreateMinutiaeData(FingerprintRecord record)
    {
        return new
        {
            Header = new
            {
                FormatIdentifier = Encoding.ASCII.GetString(record.Header.FormatIdentifier.ToArray()).TrimEnd('\0'),
                VersionNumber = Encoding.ASCII.GetString(record.Header.VersionNumber.ToArray()).TrimEnd('\0'),
                RecordLength = record.Header.RecordLength,
                NumberOfFingerRecords = record.Header.NumberOfFingerRecords,
                ProductOwner = $"0x{record.Header.ProductOwner:X4}",
                ProductType = $"0x{record.Header.ProductType:X4}",
                CaptureEquipmentCompliance = $"0x{record.Header.GetCaptureEquipmentComplianceValue():X}",
                CaptureEquipmentId = record.Header.GetCaptureEquipmentId(),
                ImageSize = new { Width = record.Header.ImageSizeX, Height = record.Header.ImageSizeY },
                Resolution = new { X = record.Header.XResolution, Y = record.Header.YResolution }
            },
            Summary = new
            {
                TotalMinutiae = record.TotalMinutiae,
                IsEmptyTemplate = record.IsEmptyTemplate,
                QualitySummary = record.GetQualitySummary()
            },
            ViewRecords = record.ViewRecords.Select(view => new
            {
                FingerPosition = view.FingerPosition,
                FingerPositionName = GetFingerPositionName(view.FingerPosition),
                ViewNumber = view.ViewNumber,
                ImpressionType = view.ImpressionType,
                ImpressionTypeName = GetImpressionTypeName(view.ImpressionType),
                FingerQuality = view.FingerQuality,
                NumberOfMinutiae = view.NumberOfMinutiae,
                Minutiae = view.Minutiae.Select(minutia => new
                {
                    Type = minutia.MinutiaType,
                    TypeName = GetMinutiaTypeName(minutia.MinutiaType),
                    XCoordinate = minutia.XCoordinate,
                    YCoordinate = minutia.YCoordinate,
                    Angle = minutia.Angle,
                    Quality = minutia.Quality
                }).ToArray()
            }).ToArray()
        };
    }
    
    private static ImmutableDictionary<string, object> CreateMetadata(FingerprintRecord record)
    {
        return ImmutableDictionary<string, object>.Empty
            .Add("FormatIdentifier", Encoding.ASCII.GetString(record.Header.FormatIdentifier.ToArray()).TrimEnd('\0'))
            .Add("VersionNumber", Encoding.ASCII.GetString(record.Header.VersionNumber.ToArray()).TrimEnd('\0'))
            .Add("RecordLength", record.Header.RecordLength)
            .Add("NumberOfFingerRecords", record.Header.NumberOfFingerRecords)
            .Add("TotalMinutiae", record.TotalMinutiae)
            .Add("IsEmptyTemplate", record.IsEmptyTemplate)
            .Add("ProductOwner", record.Header.ProductOwner)
            .Add("ProductType", record.Header.ProductType)
            .Add("CaptureEquipmentCompliance", record.Header.GetCaptureEquipmentComplianceValue())
            .Add("CaptureEquipmentId", record.Header.GetCaptureEquipmentId())
            .Add("ImageSizeX", record.Header.ImageSizeX)
            .Add("ImageSizeY", record.Header.ImageSizeY)
            .Add("XResolution", record.Header.XResolution)
            .Add("YResolution", record.Header.YResolution);
    }
    
    private static string GetFingerPositionName(byte position) => position switch
    {
        0x00 => "Unknown",
        0x01 => "Right thumb",
        0x02 => "Right index finger",
        0x03 => "Right middle finger", 
        0x04 => "Right ring finger",
        0x05 => "Right little finger",
        0x06 => "Left thumb",
        0x07 => "Left index finger",
        0x08 => "Left middle finger",
        0x09 => "Left ring finger",
        0x0A => "Left little finger",
        _ => $"Invalid ({position})"
    };
    
    private static string GetImpressionTypeName(byte impressionType) => impressionType switch
    {
        0x00 => "Live-scan plain",
        0x01 => "Live-scan rolled",
        0x02 => "Non-live-scan plain",
        0x03 => "Non-live-scan rolled",
        0x08 => "Swipe",
        0x09 => "Live-scan vertical swipe",
        _ => $"Unknown ({impressionType})"
    };
    
    private static string GetMinutiaTypeName(byte type) => type switch
    {
        0x01 => "Ridge ending",
        0x02 => "Ridge bifurcation", 
        0x00 => "Other",
        _ => $"Unknown ({type})"
    };
}