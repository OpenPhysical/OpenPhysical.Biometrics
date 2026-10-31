using System.Drawing;
using CoreJ2K;
using CoreJ2K.Skia;
using CoreJ2K.Util;
using JetBrains.Annotations;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Service for encoding images to JPEG 2000 format with ROI support for PIV compliance.
/// </summary>
[PublicAPI]
public class Jpeg2000EncoderService : IJpeg2000Encoder
{
    private readonly ILogger<Jpeg2000EncoderService> _logger;
    private static bool _isRegistered;
    private static readonly object RegisterLock = new object();

    /// <summary>
    /// Service for encoding images to JPEG 2000 format with ROI Inner Region for PIV compliance.
    /// Implements methods for region-based encoding with configurable parameters.
    /// </summary>
    public Jpeg2000EncoderService(ILogger<Jpeg2000EncoderService> logger)
    {
        _logger = logger;
        _logger.LogDebug(
            new EventId(37900, "Jpeg2000EncoderService"),
            "Jpeg2000EncoderService constructor called"
        );
    }

    private static void EnsureSkiaRegistered()
    {
        lock (RegisterLock)
        {
            if (_isRegistered)
                return;
            // Publish successful registration only after the adapter is available.
            ImageFactory.Register(new SKBitmapImageCreator());
            _isRegistered = true;
        }
    }

    /// <summary>
    /// Encodes an image to JPEG 2000 with ROI Inner Region for PIV compliance.
    /// Uses JPEG 2000 maxshift ROI encoding with configurable quality balance.
    /// </summary>
    public Result<byte[]> EncodeWithRoi(
        RgbImage image,
        FacialRoiSet roiSet,
        float baseRate = 1.0f,
        int roiStartLevel = 1,
        bool enableRoi = false,
        bool roiAlign = true
    )
    {
        _logger.LogDebug(
            new EventId(37905, "Jpeg2000EncoderService"),
            "Jpeg2000EncoderService processing started."
        );

        try
        {
            // Ensure the Skia image adapter is registered
            EnsureSkiaRegistered();

            // Get default encoder parameter list from CoreJ2K
            _logger.LogDebug(
                new EventId(37906, "Jpeg2000EncoderService"),
                "Getting default encoder parameters from J2kImage"
            );
            var parameters = J2kImage.GetDefaultEncoderParameterList();

            // Build ROI specification only if ROI is enabled
            if (enableRoi)
            {
                // Appendix C.6 approach: single Inner Region with higher priority
                var innerRegion = roiSet.InnerRegion;
                var roiSpec =
                    $"R {innerRegion.BoundingBox.X} {innerRegion.BoundingBox.Y} "
                    + $"{innerRegion.BoundingBox.Width} {innerRegion.BoundingBox.Height}";

                parameters["Rroi"] = roiSpec;
                _logger.LogDebug(
                    new EventId(37907, "Jpeg2000EncoderService"),
                    "Jpeg2000EncoderService processing status changed."
                );

                // Control ROI resolution level priority
                // Higher values include more resolution levels in ROI priority, reducing quality difference
                // 0 = only base level (aggressive ROI priority), 1-2 = more balanced quality
                parameters["Rstart_level"] = roiStartLevel.ToString();
                _logger.LogDebug(
                    new EventId(37908, "Jpeg2000EncoderService"),
                    "Jpeg2000EncoderService processing started."
                );

                // Use block alignment for more balanced quality distribution
                // With small tiles, alignment gives precise ROI control
                parameters["Ralign"] = roiAlign ? "on" : "off";
                _logger.LogDebug(
                    new EventId(37909, "Jpeg2000EncoderService"),
                    "Jpeg2000EncoderService processing status changed."
                );
            }
            else
            {
                _logger.LogDebug(new EventId(37910, "Jpeg2000EncoderService"), "ROI disabled");
            }

            // Set base compression rate - higher values improve overall quality
            // This affects both ROI and background areas proportionally
            parameters["rate"] = baseRate.ToString("F1");
            _logger.LogDebug(
                new EventId(37911, "Jpeg2000EncoderService"),
                "Jpeg2000EncoderService processing status changed."
            );

            // Use single tile covering entire PIV image for optimal compression efficiency
            // Single tile eliminates tile boundary artifacts and maximizes compression
            parameters["Stiles"] = $"{image.Width} {image.Height}";
            _logger.LogDebug(
                new EventId(37912, "Jpeg2000EncoderService"),
                "Jpeg2000EncoderService processing status changed."
            );

            // Encode with ROI parameters
            _logger.LogDebug(
                new EventId(37913, "Jpeg2000EncoderService"),
                "Starting J2kImage encoding"
            );
            using var bitmap = image.ToSkiaBitmap();
            var encodedData = J2kImage.ToBytes(bitmap, parameters);
            _logger.LogDebug(
                new EventId(37914, "Jpeg2000EncoderService"),
                "Jpeg2000EncoderService processing completed."
            );

            _logger.LogInformation(
                new EventId(37915, "Jpeg2000EncoderService"),
                "Jpeg2000EncoderService processing completed."
            );
            return Result.Success(encodedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(37916, "Jpeg2000EncoderService"),
                "JPEG 2000 ROI encoding failed"
            );
            return Result.Failure<byte[]>($"JPEG 2000 ROI encoding failed: {ex.Message}");
        }
        finally
        {
            _logger.LogDebug(new EventId(37917, "Jpeg2000EncoderService"), "EncodeWithRoi ended");
        }
    }
}
