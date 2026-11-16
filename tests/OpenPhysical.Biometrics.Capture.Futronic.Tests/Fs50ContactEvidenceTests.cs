// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ContactEvidenceTests
{
    [TestCase(-1)]
    [TestCase(65)]
    public void RidgeBoundaryToleranceMustBeWithinItsDeclaredPixelRange(int tolerance)
    {
        Fs50EvidencePolicy
            .Create(new Fs50EvidenceOptions { RidgeBoundaryTolerance = tolerance })
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
    }

    [TestCase(34, 56, 90, 240, 1u)]
    [TestCase(140, 210, 180, 250, 27u)]
    [TestCase(100, 160, 30, 200, 92u)]
    public void RandomResidueIsNotFingerContact(
        int radiusX,
        int radiusY,
        int low,
        int high,
        uint seed
    )
    {
        var pixels = ContactEvidenceFixtures.Speckles(radiusX, radiusY, low, high, seed);
        var original = pixels.ToArray();

        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;

        contact.Present.Should().BeFalse();
        contact.ComponentCount.Should().Be(0);
        contact.Issue.Should().Be(Fs50ContactIssue.InsufficientRidgeEvidence);
        contact.Mask.Should().OnlyContain(value => value == 0);
        Fs50ExposureController.Next(220, contact, true, false, 210).Value.Should().Be(220);
        pixels.Should().Equal(original);
    }

    [Test]
    public void TooLightAuthenticRidgesAreNotEligibleForCaptureOrAutomaticDarkening()
    {
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8", low: 235);
        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;

        contact.Present.Should().BeFalse();
        contact.ComponentCount.Should().Be(0);
        contact.Issue.Should().Be(Fs50ContactIssue.TooLight);
        Fs50ExposureController.Next(220, contact, true, false, 210).Value.Should().Be(220);
    }

    [TestCase(2, 7u)]
    [TestCase(2, 42u)]
    [TestCase(2, 99u)]
    [TestCase(5, 7u)]
    [TestCase(5, 42u)]
    [TestCase(5, 99u)]
    [TestCase(9, 7u)]
    [TestCase(9, 42u)]
    [TestCase(9, 99u)]
    public void DirectionallyStretchedRandomMarksAreNotRepeatedRidges(int stretch, uint seed)
    {
        var pixels = ContactEvidenceFixtures.Speckles(100, 160, 80, 230, seed, stretch);

        Fs50ContactAnalysis.Analyze(pixels).Value.Present.Should().BeFalse();
    }

    [TestCaseSource(typeof(ContactEvidenceFixtures), nameof(ContactEvidenceFixtures.PublicNames))]
    public void PublicNistRidgesRemainEligible(string name)
    {
        var contact = Fs50ContactAnalysis.Analyze(ContactEvidenceFixtures.PublicFinger(name)).Value;

        contact.Present.Should().BeTrue(name);
        contact.ComponentCount.Should().Be(1, name);
        contact.Issue.Should().Be(Fs50ContactIssue.None);
    }

    [TestCase(80, 128, 0)]
    [TestCase(64, 128, 0)]
    [TestCase(80, 128, 175)]
    [TestCase(512, 512, 175)]
    public void SmallPartialAndDryLookingPublicRidgesRemainEligible(int width, int height, int low)
    {
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8", width, height, low);

        Fs50ContactAnalysis.Analyze(pixels).Value.Present.Should().BeTrue();
    }

    [Test]
    public void AUniformDarkSmearWithStrongEdgesIsNotRidgeContact()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var y = 250; y < 500; y++)
        for (var x = 300; x < 500; x++)
            pixels[(y * 800) + x] = 100;

        Fs50ContactAnalysis.Analyze(pixels).Value.Present.Should().BeFalse();
    }

    [Test]
    public void ACoherentPatchConnectedToPaleResidueCannotBypassLightRejection()
    {
        var pixels = ContactEvidenceFixtures.Speckles(140, 210, 225, 245, 27);
        for (var y = 300; y < 428; y++)
        for (var x = 360; x < 440; x++)
            pixels[(y * 800) + x] = x % 8 < 4 ? (byte)225 : (byte)245;

        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;

        contact.Issue.Should().Be(Fs50ContactIssue.TooLight);
        contact.Present.Should().BeFalse();
        contact.Mask.Should().OnlyContain(value => value == 0);
        Fs50ExposureController.Next(220, contact, true, false, 210).Value.Should().Be(220);
    }

    [TestCase(0)]
    [TestCase(64)]
    [TestCase(128)]
    [TestCase(200)]
    [TestCase(255)]
    public void ActualCleanEmptyPlatenRemainsAbsentWithoutAWarning(int exposure)
    {
        var contact = Fs50ContactAnalysis
            .Analyze(HardwareVectorFixture.Image(exposure, "corrected"))
            .Value;

        contact.Present.Should().BeFalse();
        contact.Issue.Should().Be(Fs50ContactIssue.None);
    }
}
