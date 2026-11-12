// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PivCredentialProtocolTests
{
    [Test]
    public void ReadsFingerprintObjectWithLengthCorrectionAndResponseChaining()
    {
        var bytes = ScriptedCredentialCard.Fingerprints();
        var card = new ScriptedCredentialCard(
            [0x90, 0],
            [0x90, 0],
            [0x6C, 0x80],
            [.. bytes[..80], 0x61, 0],
            [.. bytes[80..], 0x90, 0]
        );
        var read = Read(card, "123456".AsMemory(), CancellationToken.None);
        read.Should().Succeed();
        read.Value.Record.Views.Should().ContainSingle();
        card.Commands.Count(command => command[1] == 0x20).Should().Be(1);
        card.Commands.Select(command => command[1]).Should().Equal(0xA4, 0x20, 0xCB, 0xCB, 0xC0);
        card.Commands[1].AsSpan(5).ToArray().Should().Equal(49, 50, 51, 52, 53, 54, 255, 255);
    }

    [Test]
    public void RejectedPinStopsAfterOneVerificationAndDoesNotReadBiometrics()
    {
        var card = new ScriptedCredentialCard([0x90, 0], [0x63, 0xC2]);
        Read(card, "123456".AsMemory(), CancellationToken.None)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Credential.PinRejected");
        card.Commands.Select(command => command[1]).Should().Equal(0xA4, 0x20);
    }

    [Test]
    public void InvalidPinAndCancellationSendNoCommands()
    {
        var card = new ScriptedCredentialCard();
        Read(card, "bad".AsMemory(), CancellationToken.None).Should().Fail();
        Read(card, "123456".AsMemory(), new CancellationToken(true))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Cancelled");
        card.Commands.Should().BeEmpty();
    }

    [Test]
    public void VerifyWrongLengthStatusDoesNotRetryOrModifyThePin()
    {
        var card = new ScriptedCredentialCard([0x90, 0], [0x6C, 0x08]);
        Read(card, "123456".AsMemory(), CancellationToken.None).Should().Fail();
        card.Commands.Count.Should().Be(2);
    }

    private static CSharpFunctionalExtensions.Result<TemplateDocument, DesktopError> Read(
        ICredentialCard card,
        ReadOnlyMemory<char> pin,
        CancellationToken token
    ) =>
        new CredentialReaderService(new ScriptedCredentialTransport(card))
            .ReadAsync("scripted reader", pin, token)
            .GetAwaiter()
            .GetResult();
}
