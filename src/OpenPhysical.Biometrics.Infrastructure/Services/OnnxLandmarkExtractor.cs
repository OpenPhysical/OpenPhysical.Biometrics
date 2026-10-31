using System.Drawing;
using JetBrains.Annotations;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Onnx;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// ONNX-based 68-point facial landmark extractor using PFLD model
/// </summary>
[PublicAPI]
public sealed class OnnxLandmarkExtractor : ILandmarkExtractor, IDisposable
{
    private readonly ILogger<OnnxLandmarkExtractor> _logger;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private const int ModelInputSize = 112;
    private const int NumLandmarks = 68;
    private bool _disposed;

    /// <summary>
    /// 68-point facial landmark extraction using Onnx
    /// </summary>
    /// <param name="logger"></param>
    public OnnxLandmarkExtractor(ILogger<OnnxLandmarkExtractor> logger)
    {
        _logger = logger;

        try
        {
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };

            // Load model from embedded resources
            var modelBytes = ModelRegistry.GetModel(ModelRegistry.FaceLandmarks68);
            _session = new InferenceSession(modelBytes, sessionOptions);

            // Get input/output names from the model
            _inputName = _session.InputMetadata.Keys.First();
        }
        catch (Exception)
        {
            _logger.LogError(
                new EventId(34400, "OnnxLandmarkExtractor"),
                "Failed to initialize OnnxLandmarkExtractor"
            );
            throw;
        }
    }

    /// <summary>
    /// Extracts 68 facial landmark points from a face region
    /// </summary>
    public async Task<Result<FaceLandmarks68>> ExtractLandmarksAsync(
        RgbImage image,
        FaceBox faceBox,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Step 1: Crop face region with some padding
            var paddingRatio = 0.1f; // 10% padding
            var padding = (int)(Math.Max(faceBox.Width, faceBox.Height) * paddingRatio);

            var cropX = Math.Max(0, (int)faceBox.X - padding);
            var cropY = Math.Max(0, (int)faceBox.Y - padding);
            var cropWidth = Math.Min(image.Width - cropX, (int)faceBox.Width + 2 * padding);
            var cropHeight = Math.Min(image.Height - cropY, (int)faceBox.Height + 2 * padding);

            using var prepared = PrepareModelInput(
                image,
                new Rectangle(cropX, cropY, cropWidth, cropHeight)
            );
            var faceImage = prepared.Pixels;

            // Step 3: Convert to tensor and normalize to [0, 1]
            var tensor = ImageToTensor(faceImage);

            // Step 4: Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, tensor),
            };

            using var results = await Task.Run(() => _session.Run(inputs), cancellationToken)
                .ConfigureAwait(false);
            var output = results.First().AsEnumerable<float>().ToArray();

            // Step 5: Convert output to landmarks
            if (output.Length != NumLandmarks * 2)
            {
                return Result.Failure<FaceLandmarks68>(
                    $"Invalid model output: expected {NumLandmarks * 2} values, got {output.Length}"
                );
            }

            var landmarks = new List<Point2D>();
            for (var i = 0; i < NumLandmarks; i++)
            {
                // Model outputs normalized coordinates [0, 1] in 112x112 space
                var modelX = output[i * 2] * ModelInputSize;
                var modelY = output[i * 2 + 1] * ModelInputSize;

                landmarks.Add(prepared.MapToSource(new Point2D(modelX, modelY)));
            }

            _logger.LogDebug(
                new EventId(34401, "OnnxLandmarkExtractor"),
                "OnnxLandmarkExtractor processing completed."
            );
            return Result.Success(new FaceLandmarks68(landmarks));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(34402, "OnnxLandmarkExtractor"),
                "Landmark extraction failed"
            );
            return Result.Failure<FaceLandmarks68>($"Landmark extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts an image to a tensor normalized to [0, 1]
    /// </summary>
    internal static PreparedFaceImage PrepareModelInput(RgbImage source, Rectangle crop)
    {
        var scale = Math.Min(
            (float)ModelInputSize / crop.Width,
            (float)ModelInputSize / crop.Height
        );
        var width = Math.Clamp((int)Math.Round(crop.Width * scale), 1, ModelInputSize);
        var height = Math.Clamp((int)Math.Round(crop.Height * scale), 1, ModelInputSize);
        var padX = (ModelInputSize - width) / 2;
        var padY = (ModelInputSize - height) / 2;
        using var resized = source.Clone(image => image.Crop(crop).Resize(width, height));
        var pixels = new RgbImage(ModelInputSize, ModelInputSize, Color.Black);
        try
        {
            pixels.DrawImage(resized, new Point(padX, padY));
            // Map through the actual raster dimensions, including integer padding.
            return new PreparedFaceImage(
                pixels,
                crop,
                (float)width / crop.Width,
                (float)height / crop.Height,
                padX,
                padY
            );
        }
        catch
        {
            pixels.Dispose();
            throw;
        }
    }

    internal sealed class PreparedFaceImage : IDisposable
    {
        internal PreparedFaceImage(
            RgbImage pixels,
            Rectangle crop,
            float scaleX,
            float scaleY,
            float padX,
            float padY
        )
        {
            Pixels = pixels;
            Crop = crop;
            ScaleX = scaleX;
            ScaleY = scaleY;
            PadX = padX;
            PadY = padY;
        }

        internal RgbImage Pixels { get; }
        internal Rectangle Crop { get; }
        internal float ScaleX { get; }
        internal float ScaleY { get; }
        internal float PadX { get; }
        internal float PadY { get; }

        internal Point2D MapToSource(Point2D point) =>
            new(Crop.X + (point.X - PadX) / ScaleX, Crop.Y + (point.Y - PadY) / ScaleY);

        public void Dispose() => Pixels.Dispose();
    }

    private DenseTensor<float> ImageToTensor(RgbImage image)
    {
        var tensor = new DenseTensor<float>([1, 3, ModelInputSize, ModelInputSize]);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < ModelInputSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < ModelInputSize; x++)
                {
                    var pixel = row[x];

                    // Normalize to [0, 1] and set in CHW format
                    tensor[0, 0, y, x] = pixel.R / 255f; // Red channel
                    tensor[0, 1, y, x] = pixel.G / 255f; // Green channel
                    tensor[0, 2, y, x] = pixel.B / 255f; // Blue channel
                }
            }
        });

        return tensor;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _session.Dispose();
        }
        catch (Exception)
        {
            _logger.LogError(new EventId(34403, "OnnxLandmarkExtractor"), "Error during disposal");
        }
        finally
        {
            _disposed = true;
        }
    }
}
