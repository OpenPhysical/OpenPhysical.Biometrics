using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

// The processor owns alignment; this adapter applies the caller's compression budget
// to its final pixels without repeating face detection for every encoding attempt.
internal sealed class StrategyJpeg2000Encoder(IJpeg2000Encoder encoder, ProcessingOptions options)
    : IJpeg2000Encoder
{
    public Result<byte[]> EncodeWithRoi(
        RgbImage image,
        FacialRoiSet roiSet,
        float baseRate = 1.0f,
        int roiStartLevel = 1,
        bool enableRoi = false,
        bool roiAlign = true
    )
    {
        var rate = options.Strategy switch
        {
            FixedRateStrategy fixedRate => fixedRate.Rate,
            TargetSizeStrategy target => target.TargetBytes
                * 8f
                / ((long)image.Width * image.Height),
            _ => baseRate,
        };
        if (!float.IsFinite(rate) || rate <= 0)
            return Result.Failure<byte[]>("Compression rate must be finite and greater than zero.");

        for (var attempt = 0; ; attempt++)
        {
            var result = encoder.EncodeWithRoi(
                image,
                roiSet,
                rate,
                options.RoiStartLevel,
                enableRoi,
                roiAlign
            );
            if (result.IsFailure || options.Strategy is not TargetSizeStrategy target)
                return result;
            if (result.Value.Length <= target.TargetBytes)
                return result;
            if (attempt >= options.MaxRetries)
                return Result.Failure<byte[]>(
                    "The image could not be encoded within the requested byte budget."
                );

            // Leave room for fixed container overhead when lowering the next bit rate.
            rate *= target.TargetBytes / (float)result.Value.Length * 0.9f;
        }
    }
}
