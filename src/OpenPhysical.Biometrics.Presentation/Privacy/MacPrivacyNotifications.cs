// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class MacPrivacyNotifications : IDisposable
{
    private IntPtr observer;
    private IntPtr distributed;
    private IntPtr workspace;
    private IntPtr workspaceCenter;
    private IntPtr lockName;
    private IntPtr sessionName;

    private MacPrivacyNotifications() { }

    internal static Result<MacPrivacyNotifications, DesktopError> Create(
        Func<UnitResult<DesktopError>> callback
    )
    {
        if (!OperatingSystem.IsMacOS())
            return new DesktopError(
                "Privacy.PlatformUnsupported",
                "Automatic operating-system privacy locking is available on macOS."
            );
        var value = new MacPrivacyNotifications();
        var result = DesktopEdge
            .Try(
                () => value.Register(callback),
                "Privacy.MonitorUnavailable",
                "The macOS privacy notifications are unavailable."
            )
            .Bind(inner => inner);
        if (result.IsFailure)
        {
            value.Dispose();
            return result.Error;
        }
        return value;
    }

    private UnitResult<DesktopError> Register(Func<UnitResult<DesktopError>> callback)
    {
        var created = MacPrivacyObserver.Create(callback);
        if (created.IsFailure)
            return created.Error;
        observer = created.Value;
        lockName = Name("com.apple.screenIsLocked");
        sessionName = Name("NSWorkspaceSessionDidResignActiveNotification");
        distributed = MacPrivacyNative.CFNotificationCenterGetDistributedCenter();
        workspace = MacPrivacyNative.Send(
            MacPrivacyNative.GetClass("NSWorkspace"),
            MacPrivacyNative.Selector("sharedWorkspace")
        );
        workspaceCenter = MacPrivacyNative.Send(
            workspace,
            MacPrivacyNative.Selector("notificationCenter")
        );
        if (
            lockName == IntPtr.Zero
            || sessionName == IntPtr.Zero
            || distributed == IntPtr.Zero
            || workspaceCenter == IntPtr.Zero
        )
            return new DesktopError(
                "Privacy.MonitorUnavailable",
                "The macOS privacy notification centers could not be opened."
            );
        return Subscribe();
    }

    private UnitResult<DesktopError> Subscribe()
    {
        MacPrivacyNative.CFNotificationCenterAddObserver(
            distributed,
            observer,
            MacPrivacyObserver.Distributed,
            lockName,
            IntPtr.Zero,
            4
        );
        MacPrivacyNative.AddObserver(
            workspaceCenter,
            MacPrivacyNative.Selector("addObserver:selector:name:object:"),
            observer,
            MacPrivacyNative.Selector("privacySessionResigned:"),
            sessionName,
            workspace
        );
        return UnitResult.Success<DesktopError>();
    }

    private static IntPtr Name(string value) =>
        MacPrivacyNative.CFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);

    public void Dispose()
    {
        _ = DesktopEdge.Try(
            () =>
            {
                if (observer == IntPtr.Zero)
                    return true;
                MacPrivacyObserver.Forget(observer);
                if (distributed != IntPtr.Zero && lockName != IntPtr.Zero)
                    MacPrivacyNative.CFNotificationCenterRemoveObserver(
                        distributed,
                        observer,
                        lockName,
                        IntPtr.Zero
                    );
                if (workspaceCenter != IntPtr.Zero && sessionName != IntPtr.Zero)
                    MacPrivacyNative.RemoveObserver(
                        workspaceCenter,
                        MacPrivacyNative.Selector("removeObserver:name:object:"),
                        observer,
                        sessionName,
                        workspace
                    );
                if (lockName != IntPtr.Zero)
                    MacPrivacyNative.CFRelease(lockName);
                if (sessionName != IntPtr.Zero)
                    MacPrivacyNative.CFRelease(sessionName);
                MacPrivacyNative.Send(observer, MacPrivacyNative.Selector("release"));
                observer = IntPtr.Zero;
                return true;
            },
            "Privacy.MonitorCleanup",
            "The macOS privacy observer could not be released."
        );
    }
}
