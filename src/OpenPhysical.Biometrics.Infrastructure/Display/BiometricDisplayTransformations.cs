namespace OpenPhysical.Biometrics.Infrastructure.Display;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

/// <summary>
/// Pure functional transformations from domain models to display representations.
/// </summary>
public static class BiometricDisplayTransformations
{
    /// <summary>
    /// Transforms a domain model into a display representation.
    /// </summary>
    /// <param name="domainModel">The domain model to transform.</param>
    /// <returns>A display representation or failure if the type is unknown.</returns>
    public static Result<BiometricDisplay> Transform(object domainModel) =>
        domainModel switch
        {
            FingerprintRecord fingerprint => TransformFingerprint(fingerprint),
            FacialImageRecord facial => TransformFacialImage(facial),
            _ => Result.Failure<BiometricDisplay>($"Unknown biometric type: {domainModel.GetType().Name}")
        };
    
    private static Result<BiometricDisplay> TransformFingerprint(FingerprintRecord record)
    {
        var (avgQuality, totalMinutiae, hasUnusable) = record.GetQualitySummary();
        
        var headerInfo = BiometricDisplay.Branch("Header",
            BiometricDisplay.Leaf("Format", Encoding.ASCII.GetString(record.Header.FormatIdentifier.ToArray()).TrimEnd('\0')),
            BiometricDisplay.Leaf("Version", Encoding.ASCII.GetString(record.Header.VersionNumber.ToArray()).TrimEnd('\0')),
            BiometricDisplay.Leaf("Record Length", $"{record.Header.RecordLength:N0} bytes"),
            BiometricDisplay.Leaf("Resolution", $"{record.Header.XResolution}×{record.Header.YResolution} pixels/cm"),
            BiometricDisplay.Leaf("Image Size", $"{record.Header.ImageSizeX}×{record.Header.ImageSizeY} pixels"),
            BiometricDisplay.Leaf("Product Owner", $"0x{record.Header.ProductOwner:X4}"),
            BiometricDisplay.Leaf("Product Type", $"0x{record.Header.ProductType:X4}")
        );
        
        var summaryInfo = BiometricDisplay.Branch("Summary",
            BiometricDisplay.Leaf("Total Minutiae", totalMinutiae),
            BiometricDisplay.Leaf("Finger Count", record.ViewRecords.Length),
            BiometricDisplay.Leaf("Is Empty Template", record.IsEmptyTemplate),
            BiometricDisplay.Leaf("Average Quality", avgQuality > 0 ? $"{avgQuality:F1}" : "N/A"),
            BiometricDisplay.Leaf("Has Unusable Fingers", hasUnusable)
        );
        
        var viewNodes = record.ViewRecords
            .Select((view, index) => TransformFingerView(view, index))
            .ToImmutableList();
        
        var viewsInfo = BiometricDisplay.Branch("Finger Views", viewNodes);
        
        return Result.Success(
            BiometricDisplay.Branch("INCITS 378 Fingerprint Record",
                headerInfo,
                summaryInfo,
                viewsInfo
            )
        );
    }
    
    private static BiometricDisplay TransformFingerView(FingerViewRecord view, int index)
    {
        var minutiaeInfo = view.NumberOfMinutiae > 0
            ? CreateMinutiaeInfo(view)
            : BiometricDisplay.Leaf("Minutiae", "None");
        
        return BiometricDisplay.Branch($"View {index + 1}: {GetFingerPositionName(view.FingerPosition)}",
            BiometricDisplay.Leaf("Finger Position", $"{view.FingerPosition} ({GetFingerPositionName(view.FingerPosition)})"),
            BiometricDisplay.Leaf("View Number", view.ViewNumber),
            BiometricDisplay.Leaf("Impression Type", $"{view.ImpressionType} ({GetImpressionTypeName(view.ImpressionType)})"),
            BiometricDisplay.Leaf("Quality", GetQualityDescription(view.FingerQuality)),
            BiometricDisplay.Leaf("Minutiae Count", view.NumberOfMinutiae),
            minutiaeInfo
        );
    }
    
    private static BiometricDisplay CreateMinutiaeInfo(FingerViewRecord view)
    {
        var minutiaeByType = view.Minutiae
            .GroupBy(m => m.MinutiaType)
            .Select(g => BiometricDisplay.Leaf(
                GetMinutiaTypeName(g.Key), 
                $"{g.Count()} minutiae"
            ))
            .ToImmutableList();
        
        var qualityStats = view.Minutiae.Where(m => m.Quality > 0).ToList();
        var qualityInfo = qualityStats.Any()
            ? BiometricDisplay.Branch("Quality Statistics",
                BiometricDisplay.Leaf("Average", $"{qualityStats.Average(m => m.Quality):F1}"),
                BiometricDisplay.Leaf("Min", qualityStats.Min(m => m.Quality)),
                BiometricDisplay.Leaf("Max", qualityStats.Max(m => m.Quality))
            )
            : BiometricDisplay.Leaf("Quality", "No quality data");
        
        return BiometricDisplay.Branch("Minutiae Details",
            BiometricDisplay.Branch("Types", minutiaeByType),
            qualityInfo
        );
    }
    
    private static Result<BiometricDisplay> TransformFacialImage(FacialImageRecord record)
    {
        var headerInfo = BiometricDisplay.Branch("Header",
            BiometricDisplay.Leaf("Format", record.Header.FormatIdentifier),
            BiometricDisplay.Leaf("Version", record.Header.VersionNumber),
            BiometricDisplay.Leaf("Record Length", $"{record.Header.RecordLength:N0} bytes"),
            BiometricDisplay.Leaf("Number of Faces", record.Header.NumberOfFaces)
        );
        
        var facialInfo = BiometricDisplay.Branch("Facial Information",
            BiometricDisplay.Leaf("Gender", GetGenderName(record.FacialInfo.Gender)),
            BiometricDisplay.Leaf("Eye Color", GetEyeColorName(record.FacialInfo.EyeColor)),
            BiometricDisplay.Leaf("Hair Color", GetHairColorName(record.FacialInfo.HairColor)),
            BiometricDisplay.Leaf("Expression", GetExpressionName((byte)record.FacialInfo.Expression)),
            BiometricDisplay.Leaf("Pose Angle", $"0x{record.FacialInfo.PoseAngle:X6}"),
            BiometricDisplay.Leaf("Pose Uncertainty", $"0x{record.FacialInfo.PoseAngleUncertainty:X6}")
        );
        
        var imageInfo = BiometricDisplay.Branch("Image Information",
            BiometricDisplay.Leaf("Type", GetImageTypeName(record.ImageInfo.FacialImageType)),
            BiometricDisplay.Leaf("Data Type", GetImageDataTypeName(record.ImageInfo.ImageDataType)),
            BiometricDisplay.Leaf("Dimensions", $"{record.ImageInfo.Width}×{record.ImageInfo.Height} pixels"),
            BiometricDisplay.Leaf("Color Space", GetColorSpaceName(record.ImageInfo.ImageColorSpace)),
            BiometricDisplay.Leaf("Source Type", GetSourceTypeName(record.ImageInfo.SourceType)),
            BiometricDisplay.Leaf("Device Type", $"0x{record.ImageInfo.DeviceType:X4}"),
            BiometricDisplay.Leaf("Quality", $"0x{record.ImageInfo.Quality:X4}")
        );
        
        var featurePoints = record.FeaturePoints.Any()
            ? BiometricDisplay.Branch("Feature Points",
                record.FeaturePoints
                    .Select((point, index) => BiometricDisplay.Leaf(
                        $"Point {index + 1}",
                        $"Type: {point.FeatureType}, Position: ({point.HorizontalPosition}, {point.VerticalPosition})"
                    ))
                    .ToImmutableList()
            )
            : BiometricDisplay.Leaf("Feature Points", "None");
        
        return Result.Success(
            BiometricDisplay.Branch("INCITS 385 Facial Image Record",
                headerInfo,
                facialInfo,
                imageInfo,
                featurePoints
            )
        );
    }
    
    // Helper methods for human-readable names
    private static string GetFingerPositionName(byte position) => position switch
    {
        0 => "Unknown",
        1 => "Right Thumb",
        2 => "Right Index",
        3 => "Right Middle",
        4 => "Right Ring",
        5 => "Right Little",
        6 => "Left Thumb",
        7 => "Left Index",
        8 => "Left Middle",
        9 => "Left Ring",
        10 => "Left Little",
        _ => $"Position {position}"
    };
    
    private static string GetImpressionTypeName(byte type) => type switch
    {
        0 => "Live-scan plain",
        1 => "Live-scan rolled",
        2 => "Non-live scan plain",
        3 => "Non-live scan rolled",
        8 => "Latent",
        _ => $"Type {type}"
    };
    
    private static string GetQualityDescription(byte quality) => quality switch
    {
        0 => "Quality not reported",
        >= 1 and <= 100 => $"{quality} (on 1-100 scale)",
        254 => "Finger not available",
        255 => "Finger unprintable",
        _ => $"Invalid quality value: {quality}"
    };
    
    private static string GetMinutiaTypeName(byte type) => type switch
    {
        0 => "Other",
        1 => "Ridge ending",
        2 => "Bifurcation",
        _ => $"Type {type}"
    };
    
    private static string GetGenderName(byte gender) => gender switch
    {
        0 => "Unspecified",
        1 => "Male",
        2 => "Female",
        _ => "Unknown"
    };
    
    private static string GetEyeColorName(byte color) => color switch
    {
        0 => "Unspecified",
        1 => "Black",
        2 => "Blue",
        3 => "Brown",
        4 => "Gray",
        5 => "Green",
        6 => "Multi-colored",
        _ => "Unknown"
    };
    
    private static string GetHairColorName(byte color) => color switch
    {
        0 => "Unspecified",
        1 => "Bald",
        2 => "Black",
        3 => "Blonde",
        4 => "Brown",
        5 => "Gray",
        6 => "White",
        7 => "Red",
        _ => "Unknown"
    };
    
    private static string GetExpressionName(byte expression) => expression switch
    {
        0 => "Unspecified",
        1 => "Neutral",
        2 => "Smile (closed mouth)",
        3 => "Smile (open mouth)",
        _ => "Unknown"
    };
    
    private static string GetImageTypeName(byte type) => type switch
    {
        0 => "Basic",
        1 => "Full frontal",
        2 => "Token frontal",
        _ => $"Type {type}"
    };
    
    private static string GetImageDataTypeName(byte type) => type switch
    {
        0 => "JPEG",
        1 => "JPEG 2000",
        _ => $"Type {type}"
    };
    
    private static string GetColorSpaceName(byte space) => space switch
    {
        0 => "Unspecified",
        1 => "Grayscale (8-bit)",
        2 => "RGB (24-bit)",
        _ => $"Space {space}"
    };
    
    private static string GetSourceTypeName(byte type) => type switch
    {
        0 => "Unspecified",
        1 => "Static photograph (unknown source)",
        2 => "Static photograph (digital camera)",
        3 => "Static photograph (scanner)",
        4 => "Video frame (unknown source)",
        5 => "Video frame (analog camera)",
        6 => "Video frame (digital camera)",
        _ => $"Type {type}"
    };
}