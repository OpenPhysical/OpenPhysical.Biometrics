// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioCardVerificationViewTests
{
    [AvaloniaTest]
    public async Task BoundCardActionBecomesAvailableOnlyAfterFreshAuthorityAndDisablesAfterReloadFailure()
    {
        var backend = new StudioVerificationBackendFixture();
        var model = new StudioCardVerificationViewModel(
            new StudioVerificationFactoryFixture(backend),
            new DesktopFilePicker()
        );
        var view = new StudioCardVerificationView(model);
        var window = Open(view);
        try
        {
            var action = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "VerificationCheckCard");
            action.Command.Should().BeSameAs(model.Card.PrimaryCommand);
            action.IsEffectivelyEnabled.Should().BeFalse();
            WorkspaceReviewImages.Save(window, new Size(1000, 760), "card-check-initial");
            await model.ActivateAsync();
            Dispatcher.UIThread.RunJobs();
            action.IsEffectivelyVisible.Should().BeTrue();
            action.IsEffectivelyEnabled.Should().BeTrue();
            WorkspaceReviewImages.Save(window, new Size(1000, 760), "card-check-paired");
            backend.RejectRead = true;
            await model.Setup.RefreshCommand.ExecuteAsync(default);
            Dispatcher.UIThread.RunJobs();
            action.IsEffectivelyEnabled.Should().BeFalse();
            action.IsEffectivelyVisible.Should().BeFalse();
            backend.Verifications.Should().Be(0);
        }
        finally
        {
            await model.StopAsync();
            window.Close();
        }
    }

    private static Window Open(StudioCardVerificationView view)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
