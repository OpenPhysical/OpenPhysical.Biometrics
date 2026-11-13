// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

/// <summary>One camera capture surface belonging to the current person workspace.</summary>
public sealed class FaceCaptureView : UserControl
{
    /// <summary>Loads the compiled Material facial capture controls.</summary>
    public FaceCaptureView() => AvaloniaXamlLoader.Load(this);
}
