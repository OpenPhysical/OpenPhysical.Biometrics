// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Markup.Xaml;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Shared appearance without any capture or signing services.</summary>
public class StudioPresentationApplication : Application
{
    /// <summary>Gets the native application name before its menu is initialized.</summary>
    protected virtual string ApplicationDisplayName => "OpenPhysical Biometric Studio";

    /// <inheritdoc />
    public override void Initialize()
    {
        Name = ApplicationDisplayName;
        AvaloniaXamlLoader.Load(this);
    }
}
