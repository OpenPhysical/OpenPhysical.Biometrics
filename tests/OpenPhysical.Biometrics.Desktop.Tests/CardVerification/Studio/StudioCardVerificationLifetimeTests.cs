// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioCardVerificationLifetimeTests
{
    [AvaloniaTest]
    public async Task PauseCancelsAndDrainsBeforeDisposingTheRuntimeAndCanReopenASeparateRuntime()
    {
        var first = new StudioVerificationBackendFixture { HoldUntilReleased = true };
        var second = new StudioVerificationBackendFixture();
        var factory = new StudioVerificationFactoryFixture(first, second);
        var model = new StudioCardVerificationViewModel(factory, new DesktopFilePicker());
        await model.ActivateAsync();
        model.Card.Input.ChoiceIndex = 1;
        model.Card.Input.ReaderIndex = 0;
        model.Card.Input.Pin = "123456";
        var checking = model.Card.PrimaryCommand.ExecuteAsync(default);
        await first.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var pausing = model.PauseAsync();
        await first.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        first.Disposals.Should().Be(0);
        pausing.IsCompleted.Should().BeFalse();
        first.Release.TrySetResult();
        await checking;
        (await pausing).Should().Succeed();
        first.Disposals.Should().Be(1);
        first.DisposedBeforeDrain.Should().BeFalse();
        first.LastPin.Span.ToArray().Should().OnlyContain(value => value == 0);
        model.Card.PrimaryCommand.CanExecute(default).Should().BeFalse();
        (await model.ActivateAsync()).Should().Succeed();
        factory.Opens.Should().Be(2);
        model.Setup.Ready.Should().BeTrue();
        (await model.StopAsync()).Should().Succeed();
        second.Disposals.Should().Be(1);
    }

    [AvaloniaTest]
    public async Task GuidancePresentsOnlyAuthenticatedFingerChoicesAndRequiresExplicitContinuation()
    {
        var backend = new StudioVerificationBackendFixture
        {
            FingerChoices = [new(2, 0), new(7, 1)],
        };
        var model = new StudioCardVerificationViewModel(
            new StudioVerificationFactoryFixture(backend),
            new DesktopFilePicker()
        );
        await model.ActivateAsync();
        model.Card.Input.ChoiceIndex = 2;
        model.Card.Input.ReaderIndex = 0;
        model.Card.Input.ScannerIndex = 0;
        model.Card.Input.Pin = "123456";
        var checking = model.Card.PrimaryCommand.ExecuteAsync(default);
        await backend.GuidanceRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        model.Card.AwaitingFinger.Should().BeTrue();
        model.Card.Choices.Should().HaveCount(2);
        backend.LastFinger.HasNoValue.Should().BeTrue();
        model.Card.FingerIndex = 1;
        model.Card.ContinueFingerCommand.Execute(default);
        await checking;
        backend.LastFinger.Value.Should().Be(new GuidedFingerChoice(7, 1));
        model.Card.AwaitingFinger.Should().BeFalse();
        backend.LastPin.Span.ToArray().Should().OnlyContain(value => value == 0);
        await model.StopAsync();
    }
}
