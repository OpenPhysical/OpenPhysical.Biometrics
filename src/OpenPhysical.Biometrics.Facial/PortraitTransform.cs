// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>A uniform source-to-output affine transform.</summary>
/// <param name="A">Scaled cosine of the correction angle.</param>
/// <param name="B">Scaled sine of the correction angle.</param>
/// <param name="TranslateX">Horizontal translation in output pixels.</param>
/// <param name="TranslateY">Vertical translation in output pixels.</param>
public sealed record PortraitTransform(double A, double B, double TranslateX, double TranslateY)
{
    internal bool IsInvertible
    {
        get
        {
            var norm = A * A + B * B;
            return double.IsFinite(norm) && norm > 0;
        }
    }

    /// <summary>Maps a source position into the output portrait.</summary>
    /// <param name="point">Source position.</param>
    /// <returns>Output position.</returns>
    public PortraitPoint Map(PortraitPoint point) =>
        new(A * point.X + B * point.Y + TranslateX, -B * point.X + A * point.Y + TranslateY);

    /// <summary>Maps an output position into the source image.</summary>
    /// <param name="point">Output position.</param>
    /// <returns>Source position, or a failure when the transform is singular.</returns>
    public Result<PortraitPoint, FacialError> Inverse(PortraitPoint point) =>
        IsInvertible
            ? InverseUnchecked(point).Succeed()
            : FacialErrors.SingularTransform().Fail<PortraitPoint>();

    internal PortraitPoint InverseUnchecked(PortraitPoint point)
    {
        var norm = A * A + B * B;
        var x = point.X - TranslateX;
        var y = point.Y - TranslateY;
        return new((A * x - B * y) / norm, (B * x + A * y) / norm);
    }
}
