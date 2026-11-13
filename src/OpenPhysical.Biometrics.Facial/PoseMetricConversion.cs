// SPDX-License-Identifier: Apache-2.0
// Google MediaPipe screen-to-metric conversion; see Notices/Google-Face-Geometry-Apache-2.0.txt.
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using MathNet.Numerics.LinearAlgebra;

internal static class PoseMetricConversion
{
    internal static Result<PortraitPoseEstimate, FacialError> Estimate(
        PoseCanonicalModel model,
        ImmutableArray<FaceMeshPoint> points,
        int width,
        int height,
        PortraitPoseOptions options
    )
    {
        var screen = Project(points, width, height, options);
        var offset = screen.Row(2).Average();
        var first = screen.Clone();
        first.SetRow(2, -first.Row(2));
        return PoseWeightedFit
            .Solve(model, first)
            .Bind(fit =>
                PoseWeightedFit
                    .Solve(model, Unproject(screen, offset, fit.Scale))
                    .Map(second => fit.Scale * second.Scale)
            )
            .Bind(scale => PoseWeightedFit.Solve(model, Unproject(screen, offset, scale)))
            .Map(fit => Angles(fit.Rotation, options));
    }

    private static Matrix<double> Project(
        ImmutableArray<FaceMeshPoint> points,
        int width,
        int height,
        PortraitPoseOptions options
    )
    {
        var vertical = 2 * Math.Tan(options.VerticalFieldOfViewDegrees * Math.PI / 360);
        var horizontal = vertical * width / height;
        return Matrix<double>.Build.Dense(
            3,
            468,
            (row, i) =>
                row switch
                {
                    0 => (points[i].X / width - .5) * horizontal,
                    1 => (.5 - points[i].Y / height) * vertical,
                    _ => points[i].Z / width * horizontal,
                }
        );
    }

    private static Matrix<double> Unproject(Matrix<double> screen, double offset, double scale)
    {
        var metric = screen.Clone();
        for (var i = 0; i < metric.ColumnCount; i++)
        {
            var depth = (screen[2, i] - offset + 1) / scale;
            metric[0, i] *= depth;
            metric[1, i] *= depth;
            metric[2, i] = -depth;
        }
        return metric;
    }

    private static PortraitPoseEstimate Angles(Matrix<double> r, PortraitPoseOptions options) =>
        new(
            Math.Atan2(r[2, 1], r[2, 2]) * 180 / Math.PI,
            Math.Asin(Math.Clamp(-r[2, 0], -1, 1)) * 180 / Math.PI,
            Math.Atan2(r[1, 0], r[0, 0]) * 180 / Math.PI,
            options.VerticalFieldOfViewDegrees
        );
}
