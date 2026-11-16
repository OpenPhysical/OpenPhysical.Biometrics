// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Runtime.InteropServices;

internal static class MacPrivacyNative
{
    private const string CoreFoundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DistributedCallback(
        IntPtr center,
        IntPtr observer,
        IntPtr name,
        IntPtr value,
        IntPtr userInfo
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void WorkspaceCallback(IntPtr observer, IntPtr selector, IntPtr notification);

    [DllImport(CoreFoundation, ExactSpelling = true)]
    internal static extern IntPtr CFNotificationCenterGetDistributedCenter();

    [DllImport(CoreFoundation, ExactSpelling = true)]
    internal static extern void CFNotificationCenterAddObserver(
        IntPtr center,
        IntPtr observer,
        DistributedCallback callback,
        IntPtr name,
        IntPtr value,
        nint behavior
    );

    [DllImport(CoreFoundation, ExactSpelling = true)]
    internal static extern void CFNotificationCenterRemoveObserver(
        IntPtr center,
        IntPtr observer,
        IntPtr name,
        IntPtr value
    );

    [DllImport(CoreFoundation, ExactSpelling = true)]
    internal static extern IntPtr CFStringCreateWithCString(
        IntPtr allocator,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        uint encoding
    );

    [DllImport(CoreFoundation, ExactSpelling = true)]
    internal static extern void CFRelease(IntPtr value);

    [DllImport(ObjectiveC, EntryPoint = "objc_getClass", ExactSpelling = true)]
    internal static extern IntPtr GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "sel_registerName", ExactSpelling = true)]
    internal static extern IntPtr Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_allocateClassPair", ExactSpelling = true)]
    internal static extern IntPtr AllocateClass(
        IntPtr superclass,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        nuint extraBytes
    );

    [DllImport(ObjectiveC, EntryPoint = "objc_registerClassPair", ExactSpelling = true)]
    internal static extern void RegisterClass(IntPtr value);

    [DllImport(ObjectiveC, EntryPoint = "class_addMethod", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool AddMethod(
        IntPtr type,
        IntPtr selector,
        IntPtr callback,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string encoding
    );

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    internal static extern IntPtr Send(IntPtr value, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    internal static extern void AddObserver(
        IntPtr center,
        IntPtr selector,
        IntPtr observer,
        IntPtr method,
        IntPtr name,
        IntPtr value
    );

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    internal static extern void RemoveObserver(
        IntPtr center,
        IntPtr selector,
        IntPtr observer,
        IntPtr name,
        IntPtr value
    );
}
