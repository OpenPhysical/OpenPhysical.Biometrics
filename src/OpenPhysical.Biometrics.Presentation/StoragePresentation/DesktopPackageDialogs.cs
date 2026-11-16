// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Owns transient passphrase dialogs and cancels them before application shutdown.</summary>
public sealed class DesktopPackageDialogs : IDisposable
{
    private Maybe<PackageUnlockWindow> active;
    private bool disposed;

    /// <summary>Connects a transient prompt to the owning Studio window.</summary>
    /// <param name="owner">Native dialog owner.</param>
    /// <param name="protection">In-memory package protection.</param>
    /// <returns>The prompt attachment outcome.</returns>
    public UnitResult<DesktopError> Attach(Window owner, PackageProtectionViewModel protection) =>
        protection.AttachPrompt(_ => RequestAsync(owner, protection));

    private async Task<bool> RequestAsync(Window owner, PackageProtectionViewModel protection)
    {
        if (disposed || active.HasValue)
            return false;
        var window = new PackageUnlockWindow(protection);
        active = window;
        try
        {
            return await window.ShowDialog<bool>(owner);
        }
        finally
        {
            active = Maybe<PackageUnlockWindow>.None;
        }
    }

    /// <summary>Closes any prompt so pending save and open operations can drain.</summary>
    public void Dispose()
    {
        disposed = true;
        active.Tap(window => window.Close(false));
        active = Maybe<PackageUnlockWindow>.None;
    }
}
