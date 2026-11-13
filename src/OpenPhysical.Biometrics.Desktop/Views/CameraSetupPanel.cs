// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Optical qualification settings for the actual selected camera and mode.</summary>
public sealed class CameraSetupPanel : UserControl
{
    /// <summary>Loads the compiled optical measurement controls.</summary>
    public CameraSetupPanel() => AvaloniaXamlLoader.Load(this);
}
