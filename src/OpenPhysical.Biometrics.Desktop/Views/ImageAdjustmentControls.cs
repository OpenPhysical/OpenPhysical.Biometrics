// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

/// <summary>Shared brightness and contrast controls for preferences and local capture drafts.</summary>
public sealed class ImageAdjustmentControls : UserControl
{
    /// <summary>Creates image controls bound to an ImageAdjustmentViewModel.</summary>
    public ImageAdjustmentControls() => AvaloniaXamlLoader.Load(this);
}
