// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintPreprocessingPolicyTests
{
    [Test]
    public void DefaultsAreExplicitValidatedApplicationPolicy()
    {
        var policy = FingerprintPreprocessingPolicy.Create().Value;

        policy.ClaheEnabled.Should().BeTrue();
        policy.TileSize.Should().Be(32);
        policy.ClipLimit.Should().Be(2);
        policy.Blend.Should().Be(0.5);
        policy.Brightness.Should().Be(0);
        policy.Contrast.Should().Be(20);
        policy.ContrastGain.Should().Be(1.3);
        policy.IsOriginal.Should().BeFalse();
    }

    [TestCase(-101, 0)]
    [TestCase(101, 0)]
    [TestCase(0, -1)]
    [TestCase(0, 101)]
    public void OperatorControlsAreBounded(int brightness, int contrast) =>
        FingerprintPreprocessingPolicy
            .Create(brightness: brightness, contrast: contrast)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);

    [Test]
    public void NonfiniteOrUnboundedNormalizationPoliciesFailExplicitly()
    {
        FingerprintPreprocessingPolicy.Create(tileSize: 0).Should().Fail();
        FingerprintPreprocessingPolicy.Create(clipLimit: double.NaN).Should().Fail();
        FingerprintPreprocessingPolicy.Create(clipLimit: double.PositiveInfinity).Should().Fail();
        FingerprintPreprocessingPolicy.Create(clipLimit: 0).Should().Fail();
        FingerprintPreprocessingPolicy.Create(blend: double.NaN).Should().Fail();
        FingerprintPreprocessingPolicy.Create(blend: -0.1).Should().Fail();
        FingerprintPreprocessingPolicy.Create(blend: 1.1).Should().Fail();
    }

    [Test]
    public void MaskOwnsBinaryPlanesAndRejectsInnerPixelsOutsideRetainedArea()
    {
        byte[] retained = [1, 1, 0, 0];
        var mask = FingerprintProcessingMask.Create(2, 2, retained, [1, 0, 0, 0]).Value;
        retained[0] = 0;

        mask.Retained[0].Should().Be(1);
        FingerprintProcessingMask.Create(2, 2, [1, 1, 0, 0], [0, 0, 1, 0]).Should().Fail();
        FingerprintProcessingMask.Create(2, 2, [1, 1, 2, 0], [0, 0, 0, 0]).Should().Fail();
        FingerprintProcessingMask.Create(2, 2, [1], [1]).Should().Fail();
    }
}
