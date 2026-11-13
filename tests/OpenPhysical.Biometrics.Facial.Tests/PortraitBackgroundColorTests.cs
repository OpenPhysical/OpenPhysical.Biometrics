// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PortraitBackgroundColorTests
{
    [Test]
    public void MedianUsesOnlyRemovedOriginalPixelsAndHandlesEvenAndEmptySamples()
    {
        byte[] rgb = [255, 0, 0, 10, 20, 30, 200, 210, 220, 30, 40, 50, 0, 255, 0];
        var odd = PortraitBackgroundColor.Estimate(rgb, [0, 1, 1, 1, 0]);
        odd.Should().Succeed();
        odd.Value.Value.Should().Be(new PortraitBackgroundColor(30, 40, 50, 3));
        var even = PortraitBackgroundColor.Estimate(rgb, [0, 1, 1, 0, 0]);
        even.Value.Value.Should().Be(new PortraitBackgroundColor(105, 115, 125, 2));
        PortraitBackgroundColor.Estimate(rgb, [0, 0, 0, 0, 0]).Value.HasNoValue.Should().BeTrue();
    }
}
