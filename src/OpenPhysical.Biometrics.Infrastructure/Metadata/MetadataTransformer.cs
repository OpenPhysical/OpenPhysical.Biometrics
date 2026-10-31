namespace OpenPhysical.Biometrics.Infrastructure.Metadata;

using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Abstractions.Metadata;
using OpenPhysical.Domains.Images;

/// <summary>
/// Transforms image metadata into various display formats.
/// </summary>
public sealed class MetadataTransformer : IMetadataTransformer
{
    /// <summary>Presents image metadata as a hierarchical display tree.</summary>
    /// <param name="metadata">Image metadata to present.</param>
    /// <returns>The metadata representation for display or inspection.</returns>
    public Result<MetadataTreeNode> TransformToTree(ImageMetadata metadata)
    {
        try
        {
            var children = new List<MetadataTreeNode>();

            // Basic information
            children.Add(new MetadataTreeNode($"Format: {GetFormatName(metadata.Format)}"));
            children.Add(
                new MetadataTreeNode(
                    $"Dimensions: {metadata.Dimensions.Width}×{metadata.Dimensions.Height}"
                )
            );

            // Dates
            if (metadata.CreationDate.HasValue)
                children.Add(
                    new MetadataTreeNode(
                        $"Creation Date: {metadata.CreationDate.Value:yyyy-MM-dd HH:mm:ss}"
                    )
                );
            if (metadata.ModificationDate.HasValue)
                children.Add(
                    new MetadataTreeNode(
                        $"Modification Date: {metadata.ModificationDate.Value:yyyy-MM-dd HH:mm:ss}"
                    )
                );

            // Color information
            if (metadata.ColorDepth.HasValue)
                children.Add(
                    new MetadataTreeNode($"Color Depth: {metadata.ColorDepth.Value} bits")
                );
            if (metadata.ColorSpace.HasValue)
                children.Add(new MetadataTreeNode($"Color Space: {metadata.ColorSpace.Value}"));
            if (metadata.Compression.HasValue)
                children.Add(new MetadataTreeNode($"Compression: {metadata.Compression.Value}"));

            // EXIF data
            if (metadata.Exif.HasValue)
            {
                var exifResult = TransformExifToTree(metadata.Exif.Value);
                if (exifResult.IsSuccess)
                    children.Add(exifResult.Value);
            }

            // Custom tags
            if (metadata.CustomTags.Count > 0)
            {
                var customChildren = metadata
                    .CustomTags.OrderBy(t => t.Key)
                    .Select(tag => new MetadataTreeNode($"{tag.Key}: {tag.Value}"))
                    .ToList();
                children.Add(new MetadataTreeNode("Custom Tags", customChildren));
            }

            return Result.Success(new MetadataTreeNode("Image Metadata", children));
        }
        catch (Exception ex)
        {
            return Result.Failure<MetadataTreeNode>(
                $"Failed to transform metadata to tree: {ex.Message}"
            );
        }
    }

    /// <summary>Presents capture metadata as a hierarchical display tree.</summary>
    /// <param name="exif">Camera and capture metadata to present.</param>
    /// <returns>The metadata representation for display or inspection.</returns>
    public Result<MetadataTreeNode> TransformExifToTree(ExifData exif)
    {
        try
        {
            var children = new List<MetadataTreeNode>();

            // Camera information
            if (exif.Make.HasValue || exif.Model.HasValue)
            {
                var cameraInfo = new List<string>();
                if (exif.Make.HasValue)
                    cameraInfo.Add(exif.Make.Value);
                if (exif.Model.HasValue)
                    cameraInfo.Add(exif.Model.Value);
                children.Add(new MetadataTreeNode($"Camera: {string.Join(" ", cameraInfo)}"));
            }

            // Software
            if (exif.Software.HasValue)
                children.Add(new MetadataTreeNode($"Software: {exif.Software.Value}"));

            // Dates
            if (exif.DateTimeOriginal.HasValue)
                children.Add(
                    new MetadataTreeNode(
                        $"Date Taken: {exif.DateTimeOriginal.Value:yyyy-MM-dd HH:mm:ss}"
                    )
                );
            else if (exif.DateTime.HasValue)
                children.Add(
                    new MetadataTreeNode(
                        $"Date Modified: {exif.DateTime.Value:yyyy-MM-dd HH:mm:ss}"
                    )
                );

            // Camera settings
            var settingsItems = new List<string>();
            if (exif.ExposureTime.HasValue)
            {
                var exposure = exif.ExposureTime.Value;
                if (exposure < 1)
                    settingsItems.Add($"1/{(int)(1 / exposure)}s");
                else
                    settingsItems.Add($"{exposure:F1}s");
            }
            if (exif.FNumber.HasValue)
                settingsItems.Add($"f/{exif.FNumber.Value:F1}");
            if (exif.IsoSpeed.HasValue)
                settingsItems.Add($"ISO {exif.IsoSpeed.Value}");
            if (exif.FocalLength.HasValue)
                settingsItems.Add($"{exif.FocalLength.Value:F0}mm");

            if (settingsItems.Count > 0)
                children.Add(new MetadataTreeNode($"Settings: {string.Join(", ", settingsItems)}"));

            // Description
            if (exif.Description.HasValue)
                children.Add(new MetadataTreeNode($"Description: {exif.Description.Value}"));

            // Artist
            if (exif.Artist.HasValue)
                children.Add(new MetadataTreeNode($"Artist: {exif.Artist.Value}"));

            // Copyright
            if (exif.Copyright.HasValue)
                children.Add(new MetadataTreeNode($"Copyright: {exif.Copyright.Value}"));

            // GPS Location
            if (exif.GpsLocation.HasValue)
                children.Add(new MetadataTreeNode($"GPS: {exif.GpsLocation.Value}"));

            // Orientation
            if (exif.Orientation.HasValue)
                children.Add(
                    new MetadataTreeNode(
                        $"Orientation: {GetOrientationName(exif.Orientation.Value)}"
                    )
                );

            return Result.Success(new MetadataTreeNode("EXIF Data", children));
        }
        catch (Exception ex)
        {
            return Result.Failure<MetadataTreeNode>(
                $"Failed to transform EXIF to tree: {ex.Message}"
            );
        }
    }

    /// <summary>Projects image metadata into named values for display or inspection.</summary>
    /// <param name="metadata">Image metadata to present.</param>
    /// <returns>The metadata representation for display or inspection.</returns>
    public Result<IReadOnlyDictionary<string, string>> TransformToKeyValue(ImageMetadata metadata)
    {
        try
        {
            var result = new Dictionary<string, string>
            {
                ["Format"] = GetFormatName(metadata.Format),
                ["Width"] = metadata.Dimensions.Width.ToString(),
                ["Height"] = metadata.Dimensions.Height.ToString(),
            };

            if (metadata.CreationDate.HasValue)
                result["CreationDate"] = metadata.CreationDate.Value.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                );
            if (metadata.ModificationDate.HasValue)
                result["ModificationDate"] = metadata.ModificationDate.Value.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                );
            if (metadata.ColorDepth.HasValue)
                result["ColorDepth"] = $"{metadata.ColorDepth.Value} bits";
            if (metadata.ColorSpace.HasValue)
                result["ColorSpace"] = metadata.ColorSpace.Value;
            if (metadata.Compression.HasValue)
                result["Compression"] = metadata.Compression.Value;

            // Note: JPEG-specific metadata would need to be passed separately

            // EXIF summary
            if (metadata.Exif.HasValue)
            {
                var exif = metadata.Exif.Value;
                if (exif.Make.HasValue)
                    result["ExifMake"] = exif.Make.Value;
                if (exif.Model.HasValue)
                    result["ExifModel"] = exif.Model.Value;
                if (exif.DateTimeOriginal.HasValue)
                    result["ExifDateTaken"] = exif.DateTimeOriginal.Value.ToString(
                        "yyyy-MM-dd HH:mm:ss"
                    );
                if (exif.GpsLocation.HasValue)
                    result["ExifGPS"] = exif.GpsLocation.Value.ToString();
            }

            return Result.Success<IReadOnlyDictionary<string, string>>(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyDictionary<string, string>>(
                $"Failed to transform metadata to key-value: {ex.Message}"
            );
        }
    }

    private static string GetFormatName(ImageFormat format) =>
        format switch
        {
            ImageFormat.Jpeg => "JPEG",
            ImageFormat.Jpeg2000 => "JPEG 2000",
            ImageFormat.Png => "PNG",
            ImageFormat.Tiff => "TIFF",
            ImageFormat.Bmp => "BMP",
            _ => "Unknown",
        };

    private static string GetOrientationName(int orientation) =>
        orientation switch
        {
            1 => "Normal",
            2 => "Flipped Horizontal",
            3 => "Rotated 180°",
            4 => "Flipped Vertical",
            5 => "Flipped Horizontal, Rotated 90° CW",
            6 => "Rotated 90° CW",
            7 => "Flipped Horizontal, Rotated 90° CCW",
            8 => "Rotated 90° CCW",
            _ => $"Unknown ({orientation})",
        };
}
