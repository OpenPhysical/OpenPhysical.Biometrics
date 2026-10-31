using System.Drawing;
using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Core.Abstractions;

/// <summary>
/// Simple interface for face detection services
/// </summary>
[PublicAPI]
public interface IFaceDetector
{
    /// <summary>
    /// Detects faces in an image
    /// </summary>
    /// <param name="image">Owned sRGB pixel buffer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected faces</returns>
    Task<Result<IReadOnlyList<DetectedFace>>> DetectFacesAsync(
        RgbImage image,
        CancellationToken cancellationToken = default
    );
}
