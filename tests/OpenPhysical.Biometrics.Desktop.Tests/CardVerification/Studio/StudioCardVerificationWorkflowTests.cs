// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioCardVerificationWorkflowTests
{
    [AvaloniaTest]
    public async Task MissingAuthorityShowsConnectionStepAndCannotStartACardCheck()
    {
        var backend = new StudioVerificationBackendFixture
        {
            State = StudioVerificationState.Empty,
        };
        var factory = new StudioVerificationFactoryFixture(backend);
        var model = new StudioCardVerificationViewModel(factory, new DesktopFilePicker());
        model.Card.PrimaryCommand.CanExecute(default).Should().BeFalse();
        factory.Opens.Should().Be(0);
        (await model.ActivateAsync()).Should().Succeed();
        model.Setup.PrimaryAction.Should().Be("Connect to server");
        model.Setup.ShowPairing.Should().BeTrue();
        model.Card.PrimaryCommand.CanExecute(default).Should().BeFalse();
        await model.Card.PrimaryCommand.ExecuteAsync(default);
        backend.Verifications.Should().Be(0);
        (await model.StopAsync()).Should().Succeed();
    }

    [AvaloniaTest]
    public async Task ManagedCardSelectionIsExplicitAndMissingStatusCannotBecomeAcceptance()
    {
        var result = StudioVerificationStateFixture.MissingStatus();
        var backend = new StudioVerificationBackendFixture { Outcome = result };
        var model = new StudioCardVerificationViewModel(
            new StudioVerificationFactoryFixture(backend),
            new DesktopFilePicker()
        );
        await model.ActivateAsync();
        model.Card.Input.ChoiceIndex = 0;
        model.Card.Input.ReaderIndex = 0;
        await model.Card.PrimaryCommand.ExecuteAsync(default);
        backend.Verifications.Should().Be(1);
        backend.LastSelection.Value.Choice.Should().Be(StudioVerificationStateFixture.Managed);
        backend
            .LastSelection.Value.Choice.ProfileId.Should()
            .Be(CredentialVerificationProfiles.ManagedTestCardAuthentication);
        backend.LastSelection.Value.ReaderId.Should().Be(backend.State.Readers[0].ReaderId);
        model.Card.Result.Value.Should().BeSameAs(result);
        model
            .Card.Result.Value.Decision.Should()
            .Be(CredentialVerificationDecision.InsufficientEvidence);
        model.Card.Result.Value.Status.State.Should().Be(VerificationCheckState.Missing);
        await model.StopAsync();
    }

    [AvaloniaTest]
    public async Task RejectedReloadClearsPreviousAuthorityAndResultsBeforeAnyFurtherCheck()
    {
        var backend = new StudioVerificationBackendFixture
        {
            Outcome = StudioVerificationStateFixture.MissingStatus(),
        };
        var model = new StudioCardVerificationViewModel(
            new StudioVerificationFactoryFixture(backend),
            new DesktopFilePicker()
        );
        await model.ActivateAsync();
        model.Card.Input.ChoiceIndex = 0;
        model.Card.Input.ReaderIndex = 0;
        await model.Card.PrimaryCommand.ExecuteAsync(default);
        model.Card.Result.HasValue.Should().BeTrue();
        backend.RejectRead = true;
        await model.Setup.RefreshCommand.ExecuteAsync(default);
        model.Setup.Ready.Should().BeFalse();
        model.Card.Result.HasNoValue.Should().BeTrue();
        model.Card.PrimaryCommand.CanExecute(default).Should().BeFalse();
        await model.Card.PrimaryCommand.ExecuteAsync(default);
        backend.Verifications.Should().Be(1);
        await model.StopAsync();
    }

    [AvaloniaTest]
    public async Task FailedPinCheckClearsTheOwnedBufferAndRequiresFreshExplicitInput()
    {
        var backend = new StudioVerificationBackendFixture();
        var model = new StudioCardVerificationViewModel(
            new StudioVerificationFactoryFixture(backend),
            new DesktopFilePicker()
        );
        await model.ActivateAsync();
        model.Card.Input.ChoiceIndex = 1;
        model.Card.Input.ReaderIndex = 0;
        model.Card.Input.Pin = "123456";
        await model.Card.PrimaryCommand.ExecuteAsync(default);
        backend.Verifications.Should().Be(1);
        model.Card.Input.Pin.Should().BeEmpty();
        backend.LastPin.Length.Should().Be(6);
        backend.LastPin.Span.ToArray().Should().OnlyContain(value => value == 0);
        await model.Setup.RefreshCommand.ExecuteAsync(default);
        model.Card.Input.ChoiceIndex = 1;
        model.Card.Input.ReaderIndex = 0;
        await model.Card.PrimaryCommand.ExecuteAsync(default);
        backend.Verifications.Should().Be(1);
        await model.StopAsync();
    }
}
