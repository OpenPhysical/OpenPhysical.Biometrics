// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Workstation.Application.Devices;
using OpenPhysical.Workstation.Infrastructure.Security;
using OpenPhysical.Workstation.Infrastructure.Verification;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioVerificationSelectionViewTests
{
    [AvaloniaTest]
    public async Task SoleReaderAndScannerAreDisplayedAfterFreshLoadAndRefreshWithoutRestoringPin()
    {
        var backend = new StudioVerificationBackendFixture();
        var model = Model(backend);
        var view = new StudioCardVerificationView(model);
        var window = Open(view);
        try
        {
            await model.ActivateAsync();
            Flush(window);
            model.Card.Input.ChoiceIndex.Should().Be(-1);
            model.Card.Input.ReaderIndex.Should().Be(0);
            AssertSelected<WorkstationReader>(view, "Fixture reader");
            model.Card.Input.ChoiceIndex = 2;
            Flush(window);
            model.Card.Input.ScannerIndex.Should().Be(0);
            AssertSelected<WorkstationCaptureDevice>(view, "Fixture scanner");
            model.Card.Input.Pin = "123456";
            await model.Setup.RefreshCommand.ExecuteAsync(default);
            Flush(window);
            model.Card.Input.Pin.Should().BeEmpty();
            model.Card.Input.ReaderIndex.Should().Be(0);
            AssertSelected<WorkstationReader>(view, "Fixture reader");
            backend.Verifications.Should().Be(0);
        }
        finally
        {
            await model.StopAsync();
            window.Close();
        }
    }

    [AvaloniaTest]
    public async Task SoleInstalledOperatorIsSelectedAndDisplayedBeforeSignIn()
    {
        var backend = new StudioVerificationBackendFixture
        {
            State = StudioVerificationStateFixture.Ready with { SignedIn = false },
        };
        var model = Model(backend);
        var view = new StudioCardVerificationView(model);
        var window = Open(view);
        try
        {
            await model.ActivateAsync();
            Flush(window);
            model.Setup.OperatorIndex.Should().Be(0);
            AssertSelected<OperatorIdentityOption>(view, "Fixture operator");
            backend.SignIns.Should().Be(0);
        }
        finally
        {
            await model.StopAsync();
            window.Close();
        }
    }

    private static StudioCardVerificationViewModel Model(
        StudioVerificationBackendFixture backend
    ) => new(new StudioVerificationFactoryFixture(backend), new DesktopFilePicker());

    private static Window Open(Control view)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = view,
        };
        window.Show();
        Flush(window);
        return window;
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertSelected<T>(Control view, string label)
    {
        var selector = view.GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(combo => combo.Items.OfType<T>().Any());
        selector.Items.Should().ContainSingle();
        selector.SelectedIndex.Should().Be(0);
        selector.SelectedItem.Should().BeSameAs(selector.Items[0]);
        selector
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text)
            .Should()
            .Contain(label);
    }
}
