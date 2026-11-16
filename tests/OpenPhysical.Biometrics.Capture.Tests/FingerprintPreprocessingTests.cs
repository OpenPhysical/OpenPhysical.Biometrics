// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintPreprocessingTests
{
    [Test]
    public void OriginalIsAnExactOwnedAcquisitionEvenOutsideTheRetainedMask()
    {
        var frame = PresenceFrameFixture.Create([0, 64, 128, 255]);
        var mask = FingerprintProcessingMask.Create(2, 2, [1, 1, 0, 0], [1, 0, 0, 0]).Value;
        var policy = FingerprintPreprocessingPolicy.Create(false, brightness: 0, contrast: 0).Value;

        var result = FingerprintPreprocessing.Process(
            frame,
            mask,
            policy,
            7,
            CancellationToken.None
        );

        result.Should().Succeed();
        result.Value.Pixels.Should().Equal(frame.Pixels);
        result.Value.OriginalFrame.Should().BeSameAs(frame);
        result.Value.ProcessingRevision.Should().Be(7);
        result.Value.OriginalDigest.Should().Be(result.Value.ProcessedDigest);
        result
            .Value.OriginalDigest.Should()
            .Be("81f9456ee0bb909b7fb7c887c21c21f9fc45430e881dd5d92450609410fc3936");
        result.Value.PolicyDigest.Should().Be(policy.Digest);
    }

    [Test]
    public void WhiteAnchoredContrastAndBrightnessAreAppliedOnlyToRetainedPixels()
    {
        var frame = PresenceFrameFixture.Create([64, 128, 192, 255]);
        var mask = FingerprintProcessingMask.Create(2, 2, [1, 1, 0, 1], [1, 1, 0, 0]).Value;
        var policy = FingerprintPreprocessingPolicy
            .Create(false, brightness: 100, contrast: 20)
            .Value;

        var artifact = FingerprintPreprocessing
            .Process(frame, mask, policy, CancellationToken.None)
            .Value;

        artifact.Pixels.Should().Equal([90, 173, 255, 255]);
        frame.Pixels.Should().Equal([64, 128, 192, 255]);
        artifact.OriginalDigest.Should().NotBe(artifact.ProcessedDigest);
    }

    [Test]
    public void NormalizationIsDeterministicAcrossPartialTilesAndKeepsExcludedGlassWhite()
    {
        var pixels = Enumerable.Range(0, 65 * 49).Select(i => (byte)(80 + (i % 64))).ToArray();
        var retained = Enumerable.Repeat((byte)1, pixels.Length).ToArray();
        retained[32] = 0;
        var frame = PresenceFrameFixture.Create(pixels, 65, 49);
        var mask = FingerprintProcessingMask.Create(65, 49, retained, retained).Value;
        var policy = FingerprintPreprocessingPolicy.Create().Value;

        var first = FingerprintPreprocessing
            .Process(frame, mask, policy, CancellationToken.None)
            .Value;
        var second = FingerprintPreprocessing
            .Process(frame, mask, policy, CancellationToken.None)
            .Value;

        first.Pixels.Should().Equal(second.Pixels);
        first.Identity.Should().Be(second.Identity);
        first.Pixels[32].Should().Be(255);
        frame.Pixels.Should().Equal(pixels);
        first.Width.Should().Be(65);
        first.Height.Should().Be(49);
    }

    [Test]
    public void CancelledWorkCannotPublishAnArtifact()
    {
        var frame = PresenceFrameFixture.Create([0, 64, 128, 255]);
        var mask = FingerprintProcessingMask.Create(2, 2, [1, 1, 1, 1], [1, 1, 1, 1]).Value;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        FingerprintPreprocessing
            .Process(frame, mask, FingerprintPreprocessingPolicy.Create().Value, cancellation.Token)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
    }
}
