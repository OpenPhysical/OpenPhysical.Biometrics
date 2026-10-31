using System.Drawing;
using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Core.Abstractions;

/// <summary>
/// Interface for facial landmark extraction
/// </summary>
[PublicAPI]
public interface ILandmarkExtractor
{
    /// <summary>
    /// Extracts 68-point facial landmarks from a face region
    /// </summary>
    /// <param name="image">The full image containing the face</param>
    /// <param name="faceBox">The bounding box of the detected face</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>68 facial landmark points</returns>
    Task<Result<FaceLandmarks68>> ExtractLandmarksAsync(
        RgbImage image,
        FaceBox faceBox,
        CancellationToken cancellationToken = default
    );
}
