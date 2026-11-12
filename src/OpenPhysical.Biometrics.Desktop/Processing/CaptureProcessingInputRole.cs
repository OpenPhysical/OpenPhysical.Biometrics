// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Distinguishes qualified physical acquisition from declared offline image processing.</summary>
public enum CaptureProcessingInputRole
{
    /// <summary>Physical sensor processing requires an exact frozen-frame qualification.</summary>
    LiveSensor,

    /// <summary>An offline image has no claimed physical sensor qualification.</summary>
    DeclaredOfflineImage,
}
