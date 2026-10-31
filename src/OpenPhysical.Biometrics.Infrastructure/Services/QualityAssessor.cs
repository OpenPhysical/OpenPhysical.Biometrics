using System.Drawing;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Implementation of ISO/IEC 19794-5 quality assessment
/// </summary>
[PublicAPI]
public class QualityAssessor : IQualityAssessor
{
    private readonly ILogger<QualityAssessor> _logger;

    /// <summary>
    /// Initializes a new instance of the QualityAssessor class
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public QualityAssessor(ILogger<QualityAssessor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Iso19794Assessment>> AssessAsync(
        RgbImage image,
        DetectedFace face,
        FaceLandmarks68 landmarks,
        QualityAssessmentOptions options
    )
    {
        _logger.LogDebug(
            new EventId(308400, "QualityAssessor"),
            "QualityAssessor processing started."
        );

        var startTime = DateTime.UtcNow;

        var result = await QualityAssessmentPipeline.AssessAsync(image, face, landmarks, options);

        if (result.IsSuccess)
        {
            var assessment = result.Value;
            var elapsed = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                new EventId(308401, "QualityAssessor"),
                "QualityAssessor processing completed."
            );

            if (assessment.Violations.Any())
            {
                _logger.LogWarning(
                    new EventId(308402, "QualityAssessor"),
                    "QualityAssessor processing status changed."
                );
            }
        }
        else
        {
            _logger.LogError(
                new EventId(308403, "QualityAssessor"),
                "QualityAssessor processing failed."
            );
        }

        return result;
    }
}
