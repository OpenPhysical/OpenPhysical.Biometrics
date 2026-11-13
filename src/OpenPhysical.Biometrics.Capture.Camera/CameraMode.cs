// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

/// <summary>An actual advertised capture mode, without resampling.</summary>
/// <param name="Width">Native width.</param>
/// <param name="Height">Native height.</param>
/// <param name="FrameRateNumerator">Native rational frame-rate numerator.</param>
/// <param name="FrameRateDenominator">Native rational frame-rate denominator.</param>
/// <param name="PixelFormat">Exact provider pixel format.</param>
public sealed record CameraMode(
    int Width,
    int Height,
    int FrameRateNumerator,
    int FrameRateDenominator,
    string PixelFormat
)
{
    /// <summary>Gets the native frames per second.</summary>
    public double FramesPerSecond =>
        FrameRateDenominator > 0 ? (double)FrameRateNumerator / FrameRateDenominator : 0;

    /// <summary>Returns the operator-visible resolution and native frame rate.</summary>
    /// <returns>A concise mode label.</returns>
    public override string ToString() => $"{Width} × {Height} · {FramesPerSecond:0.##} fps";
}
