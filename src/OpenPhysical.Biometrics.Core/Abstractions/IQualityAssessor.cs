using System.Drawing;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Core.Abstractions;

/// <summary>
/// Interface for ISO/IEC 19794-5 quality assessment
/// </summary>
[PublicAPI]
public interface IQualityAssessor
{
    /// <summary>
    /// Assesses the quality of a facial image according to ISO/IEC 19794-5
    /// </summary>
    /// <param name="image">The image to assess</param>
    /// <param name="face">Detected face information</param>
    /// <param name="landmarks">Facial landmarks</param>
    /// <param name="options">Assessment options</param>
    /// <returns>Quality assessment result</returns>
    Task<Result<Iso19794Assessment>> AssessAsync(
        RgbImage image,
        DetectedFace face,
        FaceLandmarks68 landmarks,
        QualityAssessmentOptions options
    );
}
