// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class PortraitPoseAnalysis
{
    internal static Result<PortraitAnalysis, FacialError> Analyze(
        PortraitImage image,
        ImmutableArray<PortraitFace> faces
    )
    {
        var assessed = ImmutableArray.CreateBuilder<PortraitFace>(faces.Length);
        foreach (var face in faces)
        {
            var pose = PortraitPoseEstimator.Estimate(face.Landmarks, image.Width, image.Height);
            if (pose.IsFailure && pose.Error.Code != FacialErrorCodes.PoseUnassessable)
                return pose.Error;
            assessed.Add(face with { Pose = pose.IsSuccess ? Maybe.From(pose.Value) : default });
        }
        return new PortraitAnalysis(image, assessed.ToImmutable()).Succeed();
    }
}
