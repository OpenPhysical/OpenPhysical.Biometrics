// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture.Usb;

/// <summary>Volatile command encoding and bounded USB transfer failures.</summary>
[TestFixture]
public sealed class Fs50ProtocolTests
{
    /// <summary>Preserves maximum exposure and exact raw geometry.</summary>
    /// <returns>Asynchronous regression check.</returns>
    [Test]
    public async Task CaptureSupportsExposure255AndCompleteRawImage()
    {
        await using var transport = new FakeTransport();
        var result = await new Fs50Protocol(transport).CaptureRawAsync(255, CancellationToken.None);
        result.Should().Succeed();
        transport.Commands.Single().Should().Equal(0x6A, 255);
        result.Value.Length.Should().Be(1280 * 832);
        result.Value.All(x => x == 42).Should().BeTrue();
        transport.MaximumRequest.Should().BeLessThanOrEqualTo(65536);
    }

    /// <summary>Reads entire endpoint packets but returns only descriptor bytes.</summary>
    /// <returns>Asynchronous regression check.</returns>
    [Test]
    public async Task MetadataPaddingIsExcluded()
    {
        await using var transport = new FakeTransport();
        var result = await new Fs50Protocol(transport).ReadDescriptorAsync(CancellationToken.None);
        result.Should().Succeed();
        result.Value.Length.Should().Be(64);
        transport.MaximumRequest.Should().Be(512);
        transport.Commands.Single().Should().Equal(0xE0);
    }

    /// <summary>Never resumes a stream after a partial command or reply.</summary>
    /// <param name="failure">Injected malformed transfer.</param>
    /// <returns>Asynchronous regression check.</returns>
    [TestCase("write")]
    [TestCase("empty")]
    [TestCase("short")]
    [TestCase("oversized")]
    public async Task MalformedTransfersFailAndRequireFreshOpen(string failure)
    {
        await using var transport = new FakeTransport { Failure = failure };
        var protocol = new Fs50Protocol(transport);
        var result = await protocol.CaptureRawAsync(255, CancellationToken.None);
        result.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.TransferIncomplete);
        var count = transport.Commands.Count;
        (await protocol.CaptureRawAsync(255, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceUnavailable);
        transport.Commands.Count.Should().Be(count);
    }

    /// <summary>Reads only the calibration read opcode and detects instability.</summary>
    /// <param name="change">Whether the second read differs.</param>
    /// <returns>Asynchronous regression check.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task CalibrationIsReadTwiceWithNoPersistentWrites(bool change)
    {
        await using var transport = new FakeTransport { ChangeCalibration = change };
        var result = await new Fs50Protocol(transport).ReadCalibrationAsync(CancellationToken.None);
        if (change)
        {
            result.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.CalibrationInvalid);
        }
        else
        {
            result.Should().Succeed();
            result.Value.Length.Should().Be(4352);
        }

        transport.Commands.Count.Should().Be(136);
        transport.Commands.All(x => x[0] == 0x9D && x[1] == 0x40).Should().BeTrue();
        transport.Commands[0].Should().Equal(0x9D, 0x40, 0x40, 0);
        transport.Commands[67].Should().Equal(0x9D, 0x40, 0, 0x11);
    }

    /// <summary>Cancellation before dispatch sends no device command.</summary>
    /// <returns>Asynchronous regression check.</returns>
    [Test]
    public async Task CancelledOperationDoesNotWrite()
    {
        await using var transport = new FakeTransport();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await new Fs50Protocol(transport).CaptureRawAsync(1, cancellation.Token);
        result.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.Cancelled);
        transport.Commands.Should().BeEmpty();
    }

    /// <summary>Does not publish or continue a capture after in-flight cancellation.</summary>
    /// <returns>Asynchronous regression check.</returns>
    [Test]
    public async Task CancellationDuringTransferStopsFurtherReads()
    {
        using var cancellation = new CancellationTokenSource();
        await using var transport = new FakeTransport { CancelOnRead = cancellation };
        var protocol = new Fs50Protocol(transport);
        var result = await protocol.CaptureRawAsync(255, cancellation.Token);
        result.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.Cancelled);
        transport.ReadCount.Should().Be(1);
        (await protocol.CaptureRawAsync(255, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceUnavailable);
    }

    private sealed class FakeTransport : IUsbDeviceTransport
    {
        public Maybe<CancellationTokenSource> CancelOnRead { get; init; } =
            Maybe<CancellationTokenSource>.None;

        public int ReadCount { get; private set; }

        public string Failure { get; init; } = string.Empty;

        public bool ChangeCalibration { get; init; }

        public List<ImmutableArray<byte>> Commands { get; } = new();

        public int MaximumRequest { get; private set; }

        public ValueTask<Result<int, CaptureError>> WriteAsync(
            byte endpoint,
            ImmutableArray<byte> data,
            TimeSpan timeout,
            CancellationToken cancellationToken
        )
        {
            endpoint.Should().Be(1);
            timeout.Should().Be(TimeSpan.FromSeconds(3));
            Commands.Add(data);
            return ValueTask.FromResult(
                Result.Success<int, CaptureError>(Failure == "write" ? 0 : data.Length)
            );
        }

        public ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadAsync(
            byte endpoint,
            int maximumBytes,
            TimeSpan timeout,
            CancellationToken cancellationToken
        )
        {
            ReadCount++;
            if (CancelOnRead.HasValue)
            {
                CancelOnRead.Value.Cancel();
            }

            endpoint.Should().Be(0x82);
            timeout.Should().Be(TimeSpan.FromSeconds(5));
            MaximumRequest = Math.Max(MaximumRequest, maximumBytes);
            int length = Failure switch
            {
                "empty" => 0,
                "short" => 1,
                "oversized" => maximumBytes + 1,
                _ => maximumBytes,
            };
            byte value = ChangeCalibration && Commands.Count > 68 ? (byte)43 : (byte)42;
            var bytes = ImmutableArray.CreateRange(Enumerable.Repeat(value, length));
            return ValueTask.FromResult(Result.Success<ImmutableArray<byte>, CaptureError>(bytes));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
