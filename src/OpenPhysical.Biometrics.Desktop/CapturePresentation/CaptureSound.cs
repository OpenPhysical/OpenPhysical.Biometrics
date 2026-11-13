// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

internal static class CaptureSound
{
    // Called only after a new capture completes, never on review, adjustment or acceptance.
    internal static UnitResult<DesktopError> Play() =>
        DesktopEdge
            .Try(
                () =>
                {
                    if (OperatingSystem.IsMacOS())
                        MacBeep();
                    else if (OperatingSystem.IsWindows())
                        WindowsBeep(0);
                    return true;
                },
                "Capture.Sound",
                "Unable to play the capture sound."
            )
            .MapError(error => error)
            .Bind(_ => UnitResult.Success<DesktopError>());

    [DllImport(
        "/System/Library/Frameworks/AppKit.framework/AppKit",
        EntryPoint = "NSBeep",
        ExactSpelling = true
    )]
    private static extern void MacBeep();

    [DllImport("user32.dll", EntryPoint = "MessageBeep", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WindowsBeep(uint type);
}
