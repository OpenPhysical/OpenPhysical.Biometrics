// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioVerificationClockTests
{
    [AvaloniaTest]
    public async Task MissingTrustedClockExplainsAdministratorRestartAndCannotBeFixedBySynchronizingOrChecking()
    {
        var backend = new StudioVerificationBackendFixture
        {
            State = StudioVerificationStateFixture.Ready with { ClockReady = false },
        };
        var model = new StudioCardVerificationViewModel(
            new StudioVerificationFactoryFixture(backend),
            new DesktopFilePicker()
        );
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = new StudioCardVerificationView(model),
        };
        window.Show();
        try
        {
            await model.ActivateAsync();
            model.Setup.ClockSetupBlocked.Should().BeTrue();
            model.Setup.Ready.Should().BeFalse();
            model.Setup.PrimaryCommand.CanExecute(default).Should().BeFalse();
            model.Card.PrimaryCommand.CanExecute(default).Should().BeFalse();
            model.Setup.Status.Should().Contain("administrator").And.Contain("restart Studio");
            await model.Setup.PrimaryCommand.ExecuteAsync(default);
            await model.Card.PrimaryCommand.ExecuteAsync(default);
            backend.Synchronizations.Should().Be(0);
            backend.Verifications.Should().Be(0);
            WorkspaceReviewImages.Save(window, new Size(1000, 760), "card-check-clock-blocked");
        }
        finally
        {
            await model.StopAsync();
            window.Close();
        }
    }
}
