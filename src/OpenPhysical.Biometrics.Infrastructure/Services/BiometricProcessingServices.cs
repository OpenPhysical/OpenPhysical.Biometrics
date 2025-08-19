using OpenPhysical.Biometrics.Core.Abstractions;
using Microsoft.Extensions.Logging;
using JetBrains.Annotations;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Aggregated biometric processing services for convenient dependency injection and disposal
/// </summary>
[PublicAPI]
public sealed class BiometricProcessingServices : IDisposable
{
    /// <summary>
    /// Face detection service
    /// </summary>
    public IFaceDetector FaceDetector { get; }
    
    /// <summary>
    /// Landmark extraction service
    /// </summary>
    public ILandmarkExtractor LandmarkExtractor { get; }
    
    /// <summary>
    /// JPEG 2000 encoding service
    /// </summary>
    public IJpeg2000Encoder Jpeg2000Encoder { get; }

    /// <summary>
    /// Quality assessment service
    /// </summary>
    public IQualityAssessor QualityAssessor { get; }

    /// <summary>
    /// Initializes a new instance of BiometricProcessingServices
    /// </summary>
    public BiometricProcessingServices(
        IFaceDetector faceDetector,
        ILandmarkExtractor landmarkExtractor,
        IJpeg2000Encoder jpeg2000Encoder,
        IQualityAssessor qualityAssessor)
    {
        FaceDetector = faceDetector;
        LandmarkExtractor = landmarkExtractor;
        Jpeg2000Encoder = jpeg2000Encoder;
        QualityAssessor = qualityAssessor;
    }

    /// <summary>
    /// Creates services with default implementations (for testing or standalone usage)
    /// </summary>
    public static BiometricProcessingServices CreateDefault(ILogger? logger = null)
    {
        logger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        
        return new BiometricProcessingServices(
            new RetinaFaceDetector(logger as ILogger<RetinaFaceDetector> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RetinaFaceDetector>.Instance),
            new OnnxLandmarkExtractor(logger as ILogger<OnnxLandmarkExtractor> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<OnnxLandmarkExtractor>.Instance),
            new Jpeg2000EncoderService(logger as ILogger<Jpeg2000EncoderService> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Jpeg2000EncoderService>.Instance),
            new QualityAssessor(logger as ILogger<QualityAssessor> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<QualityAssessor>.Instance)
        );
    }

    /// <summary>
    /// Disposes all services that implement IDisposable
    /// </summary>
    public void Dispose()
    {
        if (FaceDetector is IDisposable disposableDetector)
            disposableDetector.Dispose();
        if (LandmarkExtractor is IDisposable disposableExtractor)
            disposableExtractor.Dispose();
        if (Jpeg2000Encoder is IDisposable disposableEncoder)
            disposableEncoder.Dispose();
        if (QualityAssessor is IDisposable disposableAssessor)
            disposableAssessor.Dispose();
    }
}