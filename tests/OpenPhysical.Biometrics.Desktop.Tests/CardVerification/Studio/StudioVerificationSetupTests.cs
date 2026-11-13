// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioVerificationSetupTests
{
    [AvaloniaTest]
    public async Task PairedComputerWithoutOperatorIdentityExplainsInstallationAndReloadsWithoutCredentialFallback()
    {
        var backend = new StudioVerificationBackendFixture
        {
            State = StudioVerificationStateFixture.Ready with
            {
                SignedIn = false,
                ConfigurationReady = false,
                Operators = [],
            },
        };
        var model = new StudioCardVerificationViewModel(
            new StudioVerificationFactoryFixture(backend),
            new DesktopFilePicker()
        );
        await model.ActivateAsync();
        model.Setup.ShowOperators.Should().BeFalse();
        model.Setup.PrimaryAction.Should().Be("Reload sign-in identities");
        model
            .Setup.Status.Should()
            .Contain("administrator")
            .And.Contain("install your sign-in identity");
        await model.Setup.PrimaryCommand.ExecuteAsync(default);
        backend.SignIns.Should().Be(0);
        model.Setup.Ready.Should().BeFalse();
        model.Card.PrimaryCommand.CanExecute(default).Should().BeFalse();
        await model.StopAsync();
    }
}
