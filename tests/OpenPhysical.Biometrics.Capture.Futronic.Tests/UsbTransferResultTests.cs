// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture.Usb;

[TestFixture]
internal sealed class UsbTransferResultTests
{
    [Test]
    public async Task TimeoutWithReceivedBytesIsNotAQuietEndpoint()
    {
        await using var transport = await OpenAsync();
        var read = await transport.ReadAsync(
            0x81,
            512,
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None
        );
        read.Should()
            .Succeed()
            .Which.Should()
            .HaveCount(64)
            .And.OnlyContain(value => value == 0xA5);
    }

    [Test]
    public async Task OnlyAZeroByteTimeoutReportsQuietTimeout()
    {
        await using var transport = await OpenAsync();
        (
            await transport.ReadAsync(
                0x82,
                512,
                TimeSpan.FromMilliseconds(10),
                CancellationToken.None
            )
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.TransferTimedOut);
    }

    [Test]
    public async Task DisconnectCannotBeMistakenForQuietTimeout()
    {
        await using var transport = await OpenAsync();
        (
            await transport.ReadAsync(
                0x83,
                512,
                TimeSpan.FromMilliseconds(10),
                CancellationToken.None
            )
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceUnavailable);
    }

    [Test]
    public async Task IncompleteTimedOutCommandCannotBeConsideredIssuedSuccessfully()
    {
        await using var transport = await OpenAsync();
        (
            await transport.WriteAsync(
                0x01,
                [0x10, 0x20],
                TimeSpan.FromMilliseconds(10),
                CancellationToken.None
            )
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.TransferIncomplete);
    }

    [TestCase(0x84)]
    [TestCase(0x85)]
    public async Task ImpossibleNativeByteCountsRemainFailures(byte endpoint)
    {
        await using var transport = await OpenAsync();
        (
            await transport.ReadAsync(
                endpoint,
                512,
                TimeSpan.FromMilliseconds(10),
                CancellationToken.None
            )
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceUnavailable);
    }

    private static async Task<IUsbDeviceTransport> OpenAsync()
    {
        var name =
            OperatingSystem.IsMacOS() ? "libscripted_libusb.dylib"
            : OperatingSystem.IsWindows() ? "scripted_libusb.dll"
            : "libscripted_libusb.so";
        var provider = new LibUsbDeviceProvider(
            Path.Combine(TestContext.CurrentContext.TestDirectory, name)
        );
        var devices = await provider.DiscoverAsync(0x1234, 0x5678, CancellationToken.None);
        devices.Should().Succeed();
        var opened = await provider.OpenAsync(devices.Value.Single(), CancellationToken.None);
        opened.Should().Succeed();
        return opened.Value;
    }
}
