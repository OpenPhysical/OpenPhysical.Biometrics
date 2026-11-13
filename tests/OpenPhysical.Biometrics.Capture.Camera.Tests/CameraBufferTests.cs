// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera.Tests;

using System.Buffers.Binary;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Imaging;

[TestFixture]
internal sealed class CameraBufferTests
{
    [TestCase("ARGB32")]
    [TestCase("RGB32")]
    [TestCase("RGB24")]
    public void AvFoundationRgbPreservesKnownChannelsAndTopDownRows(string format)
    {
        var packet = new byte[54 + 16];
        packet[0] = (byte)'B';
        packet[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(14), 40);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(
            packet.AsSpan(28),
            (short)(format == "RGB24" ? 24 : 32)
        );
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(18), 2);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(22), 2);
        Pixels(format).CopyTo(packet, 54);
        var result = CameraBufferDecoder.Decode(packet, new(2, 2, 30, 1, format), true);
        result.Should().Succeed();
        using var image = result.Value;
        image.CopyRgbBytes().Value.Should().Equal(255, 0, 0, 0, 255, 0, 0, 0, 255, 10, 20, 30);
        CameraBufferDecoder
            .Decode(packet, new(2, 2, 30, 1, "NV12"), true)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CameraErrorCodes.InvalidFrame);
        CameraBufferDecoder
            .Decode(packet.AsMemory(0, 60), new(2, 2, 30, 1, "ARGB32"), true)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CameraErrorCodes.InvalidFrame);
    }

    private static byte[] Pixels(string format) =>
        format switch
        {
            "ARGB32" => [0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 30, 20, 10, 255],
            "RGB32" => [255, 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 10, 20, 30],
            _ => [255, 0, 0, 0, 255, 0, 0, 0, 0, 0, 255, 10, 20, 30, 0, 0],
        };

    [Test]
    public async Task QueueDropsAndClearsOldFramesThenDrainsAndFencesLatePublication()
    {
        var buffer = new CameraFrameBuffer();
        var first = Frame(1);
        var second = Frame(2);
        buffer.Publish(first).Should().Succeed();
        buffer.Publish(second).Should().Succeed();
        first.Image.CopyRgbBytes().Should().Fail();
        (await buffer.ReadAsync(CancellationToken.None))
            .Should()
            .Succeed()
            .Which.Should()
            .BeSameAs(second);
        second.Image.CopyRgbBytes().Should().Succeed();
        var waiting = buffer.ReadAsync(CancellationToken.None);
        buffer.Complete().Should().Succeed();
        (await waiting).Should().Fail().Which.Code.Should().Be(CameraErrorCodes.Closed);
        var late = Frame(3);
        buffer.Publish(late).Should().Fail().Which.Code.Should().Be(CameraErrorCodes.Closed);
        late.Image.CopyRgbBytes().Should().Fail();
        (await buffer.ReadAsync(CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CameraErrorCodes.Closed);
        second.Dispose();
    }

    [Test]
    public async Task CancellationLeavesTheNextFrameAvailable()
    {
        var buffer = new CameraFrameBuffer();
        using var cancel = new CancellationTokenSource();
        var waiting = buffer.ReadAsync(cancel.Token);
        cancel.Cancel();
        (await waiting).Should().Fail().Which.Code.Should().Be(CameraErrorCodes.Cancelled);
        var frame = Frame(7);
        buffer.Publish(frame);
        (await buffer.ReadAsync(CancellationToken.None)).Value.Should().BeSameAs(frame);
        frame.Dispose();
        buffer.Complete();
    }

    private static CameraFrame Frame(long sequence) =>
        new(
            RgbImage.Create(2, 2, new RgbaPixel(12, 34, 56)).Value,
            new("test", "Synthetic camera", [new(2, 2, 30, 1, "RGB24")]),
            new(2, 2, 30, 1, "RGB24"),
            sequence,
            DateTimeOffset.UnixEpoch,
            TimeSpan.Zero
        );
}
