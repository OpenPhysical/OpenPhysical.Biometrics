// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class BgraInputTests
{
    [Test]
    public void NativeRowsKeepTheirChannelsOrientationAndOwnershipWithoutPadding()
    {
        byte[] source = [0, 0, 255, 17, 9, 9, 9, 9, 255, 0, 0, 23, 8, 8, 8, 8];
        var decoded = RgbImageBytes.FromBgraBytes(source, 1, 2, 8);
        decoded.Should().Succeed();
        using var image = decoded.Value;
        Array.Clear(source);
        image.CopyRgbBytes().Value.Should().Equal(255, 0, 0, 0, 0, 255);
        image.GetPixel(0, 0).Value.A.Should().Be(255);
    }

    [TestCase(0, 2, 8, 16)]
    [TestCase(2, 2, 4, 16)]
    [TestCase(1, 2, 8, 15)]
    public void IncompleteOrInvalidNativeRowsAreRejected(
        int width,
        int height,
        int stride,
        int length
    )
    {
        RgbImageBytes.FromBgraBytes(new byte[length], width, height, stride).Should().Fail();
    }
}
