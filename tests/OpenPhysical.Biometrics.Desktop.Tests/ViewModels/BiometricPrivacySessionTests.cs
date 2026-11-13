// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

[TestFixture]
internal sealed class BiometricPrivacySessionTests
{
    [AvaloniaTest]
    public async Task SuspensionForgetsProtectionBeforeAnAcquisitionPauseCompletesOrFails()
    {
        using var protection = Unlocked();
        var pending = new TaskCompletionSource<UnitResult<DesktopError>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var pauses = 0;
        using var privacy = new BiometricPrivacySession(
            protection,
            () =>
            {
                pauses++;
                return pending.Task;
            },
            NullLogger<BiometricPrivacySession>.Instance
        );
        var suspended = privacy.SuspendAsync();
        protection.IsUnlocked.Should().BeFalse();
        protection.Passphrase.Should().BeEmpty();
        suspended.IsCompleted.Should().BeFalse();
        privacy.SuspendAsync().Should().BeSameAs(suspended);
        pauses.Should().Be(1);
        pending.SetResult(new DesktopError("Camera.Disconnected", "The camera was disconnected."));
        (await suspended).Should().Fail().Which.Code.Should().Be("Camera.Disconnected");
        protection.IsUnlocked.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task DisposalForgetsProtectionAndPreventsLateSuspensionFromRestartingAcquisition()
    {
        using var protection = Unlocked();
        var pauses = 0;
        var privacy = new BiometricPrivacySession(
            protection,
            () =>
            {
                pauses++;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            NullLogger<BiometricPrivacySession>.Instance
        );
        privacy.Dispose();
        (await privacy.SuspendAsync()).Should().Succeed();
        privacy.StartMonitoring().Should().Fail().Which.Code.Should().Be("Privacy.SessionDisposed");
        protection.IsUnlocked.Should().BeFalse();
        pauses.Should().Be(0);
    }

    [AvaloniaTest]
    public void NativeRegistrationHasAnExplicitPlatformBoundaryAndCanBeDisposed()
    {
        using var protection = Unlocked();
        var pauses = 0;
        using var privacy = new BiometricPrivacySession(
            protection,
            () =>
            {
                pauses++;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            NullLogger<BiometricPrivacySession>.Instance
        );
        var started = privacy.StartMonitoring();
        if (OperatingSystem.IsMacOS())
        {
            started.Should().Succeed();
            PrivacyWorkspaceSignal.PostSessionResigned();
            protection.IsUnlocked.Should().BeFalse();
            Dispatcher.UIThread.RunJobs();
            pauses.Should().Be(1);
            privacy.Dispose();
            Unlock(protection);
            PrivacyWorkspaceSignal.PostSessionResigned();
            Dispatcher.UIThread.RunJobs();
            protection.IsUnlocked.Should().BeTrue();
            pauses.Should().Be(1);
        }
        else
            started.Should().Fail().Which.Code.Should().Be("Privacy.PlatformUnsupported");
    }

    private static PackageProtectionViewModel Unlocked()
    {
        var protection = new PackageProtectionViewModel();
        Unlock(protection);
        return protection;
    }

    private static bool Unlock(PackageProtectionViewModel protection)
    {
        protection.Passphrase = protection.Confirmation = "public privacy test passphrase";
        protection.Unlock().Should().Succeed();
        return true;
    }
}
