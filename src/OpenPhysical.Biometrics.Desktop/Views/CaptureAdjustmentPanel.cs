// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

/// <summary>Inline local accepted-capture editing with explicit Apply and Cancel.</summary>
public sealed class CaptureAdjustmentPanel : UserControl
{
    /// <summary>Creates the local capture editor.</summary>
    public CaptureAdjustmentPanel() => AvaloniaXamlLoader.Load(this);
}
