// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

/// <summary>Operator controls for live and captured image adjustments.</summary>
public sealed class ImageAdjustmentPanel : UserControl
{
    /// <summary>Creates the image adjustment controls.</summary>
    public ImageAdjustmentPanel() => AvaloniaXamlLoader.Load(this);
}
