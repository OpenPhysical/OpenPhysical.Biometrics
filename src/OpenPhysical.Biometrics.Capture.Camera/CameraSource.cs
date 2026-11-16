// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>A discovered camera and its usable native RGB or encoded-image modes.</summary>
/// <param name="Id">Opaque stable provider identity.</param>
/// <param name="Name">Operating-system device label.</param>
/// <param name="Modes">Native modes with known color interpretation.</param>
public sealed record CameraSource(string Id, string Name, ImmutableArray<CameraMode> Modes)
{
    /// <summary>Gets an advertised 1080p/30 mode where available, then the closest native alternative.</summary>
    public Result<CameraMode, CameraError> PreferredMode =>
        Modes.IsDefaultOrEmpty
            ? new CameraError(
                CameraErrorCodes.UnsupportedMode,
                "This camera has no supported RGB capture mode."
            )
            : Modes
                .OrderBy(value => value.Width == 1920 && value.Height == 1080 ? 0 : 1)
                .ThenBy(value => Math.Abs((long)value.Width * value.Height - 1920L * 1080))
                .ThenBy(value => Math.Abs(value.FramesPerSecond - 30))
                .ThenBy(value => value.PixelFormat == "ARGB32" ? 0 : 1)
                .First();

    /// <summary>Returns the system camera label.</summary>
    /// <returns>The device name.</returns>
    public override string ToString() => Name;
}
