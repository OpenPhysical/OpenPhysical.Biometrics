// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ConnectedContactTests
{
    [Test]
    public void SelectedContactIsConnectedTextureRatherThanFragmentedRidgeConfidence()
    {
        var pixels = ContactEvidenceFixtures.Speckles(140, 210, 90, 240, 27);
        _ = Paint(pixels, 350, 300, 80, 128);
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        region
            .ContactMask.Zip(region.Diagnostics.ObservedContactMask)
            .Should()
            .OnlyContain(pair => pair.First <= pair.Second);
        region.ContactMask[350 * 800 + 390].Should().Be(1);
        region.ContactMask.Sum(value => value).Should().BeGreaterThan(region.Area);
        BinaryMaskOracle
            .Components(region.ContactMask.AsSpan(), CancellationToken.None)
            .Value.Length.Should()
            .Be(1);
        region.Diagnostics.AcceptedMask.Should().Equal(region.Mask);
    }

    [Test]
    public void ContactMaskNeverUnionsAnAdditionalAcceptedComponent()
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = Paint(pixels, 180, 250, 128, 192);
        _ = Paint(pixels, 500, 300, 80, 128);
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        region.ComponentCount.Should().Be(2);
        region.ContactMask[350 * 800 + 220].Should().Be(1);
        region.ContactMask[350 * 800 + 540].Should().Be(0);
        region
            .ContactMask.Sum(value => value)
            .Should()
            .BeLessThan(region.Diagnostics.CandidateArea);
    }

    [Test]
    public void NoAcceptedContactHasNoConnectedContactEstimate()
    {
        var region = Fs50ContactAnalysis.Analyze(Fs50EvidenceFrameFixture.Empty()).Value;
        region.ContactMask.Should().OnlyContain(value => value == 0);
    }

    private static bool Paint(byte[] pixels, int left, int top, int width, int height)
    {
        for (var y = top; y < top + height; y++)
        for (var x = left; x < left + width; x++)
            pixels[y * 800 + x] = x % 8 < 4 ? (byte)40 : (byte)180;
        return true;
    }
}
