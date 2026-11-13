// SPDX-License-Identifier: Apache-2.0
// Adapted from Google MediaPipe Face Geometry; see Notices/Google-Face-Geometry-Apache-2.0.txt.
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Estimates source pose using Google's metric conversion and weighted canonical-face alignment.</summary>
public static class PortraitPoseEstimator
{
    private static readonly Lazy<Result<PoseCanonicalModel, FacialError>> Canonical = new(
        PoseCanonicalModel.Load
    );

    /// <summary>SHA-256 of the pinned canonical XYZ coordinates and landmark weights.</summary>
    public const string CanonicalModelSha256 =
        "b94647c5f575dd5a395489e233ad4142992e8fff50293c038fb70711f187c83d";

    /// <summary>Estimates pose with the documented virtual-camera defaults.</summary>
    /// <param name="landmarks">Original, unmirrored source-pixel mesh coordinates and relative depth.</param>
    /// <param name="width">Actual source width, before portrait resampling.</param>
    /// <param name="height">Actual source height, before portrait resampling.</param>
    /// <returns>Estimated angles or an explicit unassessable/model failure.</returns>
    public static Result<PortraitPoseEstimate, FacialError> Estimate(
        ImmutableArray<FaceMeshPoint> landmarks,
        int width,
        int height
    ) => Estimate(landmarks, width, height, PortraitPoseOptions.Default);

    /// <summary>Estimates pose using explicit virtual-camera parameters without altering source pixels.</summary>
    /// <param name="landmarks">Canonical 478-point topology in original source-pixel units.</param>
    /// <param name="width">Actual source width in pixels.</param>
    /// <param name="height">Actual source height in pixels.</param>
    /// <param name="options">Virtual-camera assumptions retained with the estimate.</param>
    /// <returns>Estimated angles or a typed numerical/input failure.</returns>
    public static Result<PortraitPoseEstimate, FacialError> Estimate(
        ImmutableArray<FaceMeshPoint> landmarks,
        int width,
        int height,
        PortraitPoseOptions options
    )
    {
        if (!Valid(landmarks, width, height, options))
            return PoseErrors.Unassessable();
        return FacialEdge.TryBind(
            () =>
                Canonical.Value.Bind(model =>
                    PoseMetricConversion.Estimate(model, landmarks, width, height, options)
                ),
            FacialErrorCodes.PoseUnassessable,
            "Source face pose could not be estimated."
        );
    }

    private static bool Valid(
        ImmutableArray<FaceMeshPoint> points,
        int width,
        int height,
        PortraitPoseOptions options
    ) =>
        width > 0
        && height > 0
        && (long)width * height <= PortraitImage.MaximumPixels
        && options.IsValid
        && !points.IsDefault
        && points.Length == 478
        && points.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z));
}
