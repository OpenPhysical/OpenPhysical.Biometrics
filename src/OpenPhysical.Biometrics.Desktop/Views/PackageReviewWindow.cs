// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

/// <summary>Read-only local inspection of a saved person's unsigned biometric objects.</summary>
public sealed class PackageReviewWindow : Window
{
    /// <summary>Creates the native Material inspection window.</summary>
    public PackageReviewWindow() => AvaloniaXamlLoader.Load(this);
}
