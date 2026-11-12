// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ReliableMaskTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void LocalRidgeEvidenceIsSeparateFromWholeContactAuthorization(bool hasAuthorizingPatch)
    {
        var pixels = ContactEvidenceFixtures.Speckles(140, 210, 90, 240, 27);
        if (hasAuthorizingPatch)
            _ = Paint(pixels, 300, 250, 80, 144);
        _ = Paint(pixels, 476, 456, 24, 32);

        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;
        var isolatedCenter = (472 * 800) + 488;

        contact.Diagnostics.RidgeSupportMask[isolatedCenter].Should().Be(1);
        contact.Mask[isolatedCenter].Should().Be(hasAuthorizingPatch ? (byte)1 : (byte)0);
        contact.Mask[(424 * 800) + 432].Should().Be(0);
        contact.Present.Should().Be(hasAuthorizingPatch);
    }

    [Test]
    public void ALocalRidgePatchCannotRetainAnEntireConnectedSmear()
    {
        var pixels = ContactEvidenceFixtures.Speckles(140, 210, 90, 240, 27);
        for (var y = 330; y < 426; y++)
        for (var x = 368; x < 432; x++)
            pixels[(y * 800) + x] = x % 8 < 4 ? (byte)65 : (byte)165;

        var region = Fs50ContactAnalysis.Analyze(pixels, Fs50EvidencePolicy.Default).Value;
        var diagnostics = region.Diagnostics;

        diagnostics.CandidateArea.Should().BeGreaterThan(80000);
        diagnostics.AcceptedArea.Should().BeLessThan(30000);
        diagnostics.RejectedArea.Should().BeGreaterThan(50000);
        region.Mask[375 * 800 + 290].Should().Be(0);
        region.Mask[375 * 800 + 400].Should().Be(1);
        region.ToProcessingMask().Value.Retained.Should().Equal(diagnostics.AcceptedMask);
    }

    [Test]
    public void RejectedContactRetainsPhotometricAndCandidateEvidence()
    {
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8", low: 235);

        var region = Fs50ContactAnalysis.Analyze(pixels, Fs50EvidencePolicy.Default).Value;

        region.Issue.Should().Be(Fs50ContactIssue.TooLight);
        region.Present.Should().BeFalse();
        region.Diagnostics.CandidateArea.Should().BeGreaterThan(4000);
        region.Diagnostics.Signal.Should().BeInRange(0, 30);
        region.Diagnostics.Contrast.Should().BeGreaterThan(0);
        region.Diagnostics.RejectedArea.Should().Be(region.Diagnostics.CandidateArea);
    }

    [Test]
    public void InnerGuardIsContainedAndErodesEachRetainedBoundary()
    {
        var region = Fs50ContactAnalysis
            .Analyze(ContactEvidenceFixtures.PublicFinger("f0005_03.gray8"))
            .Value;
        var mask = region.ToProcessingMask().Value;

        mask.Inner.Count(value => value == 1)
            .Should()
            .BeLessThan(mask.Retained.Count(value => value == 1));
        for (var i = 0; i < mask.Inner.Length; i++)
            if (mask.Inner[i] != 0)
                mask.Retained[i].Should().Be(1);
    }

    private static bool Paint(byte[] pixels, int left, int top, int width, int height)
    {
        for (var y = top; y < top + height; y++)
        for (var x = left; x < left + width; x++)
            pixels[(y * 800) + x] = x % 8 < 4 ? (byte)40 : (byte)200;
        return true;
    }
}
