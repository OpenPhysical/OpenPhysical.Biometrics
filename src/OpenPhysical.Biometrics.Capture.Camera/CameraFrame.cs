// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using OpenPhysical.Biometrics.Imaging;

/// <summary>An owned, unmirrored RGB acquisition; disposal clears its image pixels.</summary>
/// <param name="Image">Owned original image.</param>
/// <param name="Source">Actual camera.</param>
/// <param name="Mode">Actual native mode.</param>
/// <param name="Sequence">Native frame index.</param>
/// <param name="CapturedAt">Host acquisition time.</param>
/// <param name="DeviceTimestamp">Provider monotonic timestamp.</param>
public sealed record CameraFrame(
    RgbImage Image,
    CameraSource Source,
    CameraMode Mode,
    long Sequence,
    DateTimeOffset CapturedAt,
    TimeSpan DeviceTimestamp
) : IDisposable
{
    /// <summary>Releases and clears the owned RGB image.</summary>
    public void Dispose() => Image.Dispose();
}
