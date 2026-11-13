// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

internal static class MacPrivacyObserver
{
    private static readonly object Gate = new();
    private static readonly Lazy<IntPtr> AppKit = new(() =>
        NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit")
    );
    private static readonly ConcurrentDictionary<IntPtr, Func<UnitResult<DesktopError>>> Callbacks =
        new();
    private static readonly MacPrivacyNative.WorkspaceCallback Workspace = WorkspaceReceived;
    internal static readonly MacPrivacyNative.DistributedCallback Distributed = DistributedReceived;

    internal static Result<IntPtr, DesktopError> Create(Func<UnitResult<DesktopError>> callback)
    {
        lock (Gate)
        {
            _ = AppKit.Value;
            var type = ObserverType();
            if (type.IsFailure)
                return type.Error;
            var observer = MacPrivacyNative.Send(
                MacPrivacyNative.Send(type.Value, MacPrivacyNative.Selector("alloc")),
                MacPrivacyNative.Selector("init")
            );
            if (observer == IntPtr.Zero)
                return new DesktopError(
                    "Privacy.MonitorUnavailable",
                    "The macOS privacy observer could not be created."
                );
            Callbacks[observer] = callback;
            return observer;
        }
    }

    private static Result<IntPtr, DesktopError> ObserverType()
    {
        var type = MacPrivacyNative.GetClass("OPBiometricPrivacyObserver");
        if (type != IntPtr.Zero)
            return type;
        type = MacPrivacyNative.AllocateClass(
            MacPrivacyNative.GetClass("NSObject"),
            "OPBiometricPrivacyObserver",
            0
        );
        if (
            type == IntPtr.Zero
            || !MacPrivacyNative.AddMethod(
                type,
                MacPrivacyNative.Selector("privacySessionResigned:"),
                Marshal.GetFunctionPointerForDelegate(Workspace),
                "v@:@"
            )
        )
            return new DesktopError(
                "Privacy.MonitorUnavailable",
                "The macOS privacy observer could not be registered."
            );
        MacPrivacyNative.RegisterClass(type);
        return type;
    }

    internal static bool Forget(IntPtr observer) => Callbacks.TryRemove(observer, out _);

    private static void WorkspaceReceived(IntPtr observer, IntPtr selector, IntPtr notification) =>
        Invoke(observer);

    private static void DistributedReceived(
        IntPtr center,
        IntPtr observer,
        IntPtr name,
        IntPtr value,
        IntPtr userInfo
    ) => Invoke(observer);

    private static UnitResult<DesktopError> Invoke(IntPtr observer)
    {
        if (Callbacks.TryGetValue(observer, out var callback))
            return DesktopEdge
                .Try(
                    callback,
                    "Privacy.NotificationFailed",
                    "A privacy notification could not be handled."
                )
                .Bind(value => value);
        return UnitResult.Success<DesktopError>();
    }
}
