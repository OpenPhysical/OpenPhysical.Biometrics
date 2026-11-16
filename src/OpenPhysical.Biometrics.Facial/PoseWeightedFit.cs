// SPDX-License-Identifier: Apache-2.0
// Weighted Procrustes adaptation of Google MediaPipe; see Notices/Google-Face-Geometry-Apache-2.0.txt.
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using MathNet.Numerics.LinearAlgebra;

internal sealed record PoseWeightedFit(Matrix<double> Rotation, double Scale)
{
    internal static Result<PoseWeightedFit, FacialError> Solve(
        PoseCanonicalModel model,
        Matrix<double> target
    )
    {
        var source = model.Points;
        var center = source * model.Weights / model.Weights.Sum();
        var centered = source.Clone();
        var weightedTarget = target.Clone();
        for (var c = 0; c < source.ColumnCount; c++)
        for (var r = 0; r < 3; r++)
        {
            centered[r, c] -= center[r];
            weightedTarget[r, c] *= model.Weights[c];
        }
        var design = weightedTarget * centered.Transpose();
        if (!double.IsFinite(design.FrobeniusNorm()) || design.FrobeniusNorm() <= 1e-9)
            return PoseErrors.Unassessable();
        var svd = design.Svd(true);
        if (svd.S[1] <= 1e-9)
            return PoseErrors.Unassessable();
        var post = svd.U;
        if (post.Determinant() * svd.VT.Determinant() < 0)
            post.SetColumn(2, -post.Column(2));
        var rotation = post * svd.VT;
        var numerator = (rotation * centered).PointwiseMultiply(weightedTarget).RowSums().Sum();
        var denominator = centered.PointwiseMultiply(source) * model.Weights;
        var scale = numerator / denominator.Sum();
        return double.IsFinite(scale) && scale > 1e-9
            ? new PoseWeightedFit(rotation, scale).Succeed()
            : PoseErrors.Unassessable().Fail<PoseWeightedFit>();
    }
}
