// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Runtime.InteropServices;

internal static class PrivacyWorkspaceSignal
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    internal static bool PostSessionResigned()
    {
        var framework = NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
        try
        {
            var name = Marshal.ReadIntPtr(
                NativeLibrary.GetExport(framework, "NSWorkspaceSessionDidResignActiveNotification")
            );
            var workspace = Send(GetClass("NSWorkspace"), Selector("sharedWorkspace"));
            var center = Send(workspace, Selector("notificationCenter"));
            Post(center, Selector("postNotificationName:object:"), name, workspace);
            return true;
        }
        finally
        {
            NativeLibrary.Free(framework);
        }
    }

    [DllImport(ObjectiveC, EntryPoint = "objc_getClass", ExactSpelling = true)]
    private static extern IntPtr GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "sel_registerName", ExactSpelling = true)]
    private static extern IntPtr Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    private static extern void Post(IntPtr center, IntPtr selector, IntPtr name, IntPtr value);
}
