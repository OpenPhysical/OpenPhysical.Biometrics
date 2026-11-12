// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintClaheNumericalTests
{
    [Test]
    public void ClippedHistogramHasIndependentlyCalculatedCumulativeLevels()
    {
        var pixels = Enumerable.Range(0, 32 * 32).Select(i => (byte)(i % 32)).ToArray();
        var plane = Enumerable.Repeat((byte)1, pixels.Length).ToArray();
        var source = PresenceFrameFixture.Create(pixels, 32, 32);
        var mask = FingerprintProcessingMask.Create(32, 32, plane, plane).Value;
        var policy = FingerprintPreprocessingPolicy.Create(blend: 1, contrast: 0).Value;

        var artifact = FingerprintPreprocessing
            .Process(source, mask, policy, CancellationToken.None)
            .Value;

        // 32 counts/bin clip to8; 768 excess gives3/bin. Occupied bins therefore hold11.
        for (var value = 0; value < 32; value++)
            artifact.Pixels[value].Should().Be((byte)Math.Round((value + 1) * 11 * 255.0 / 1024));
    }

    [Test]
    public void AdjacentHistogramTilesInterpolateAtPixelCenters()
    {
        var pixels = Enumerable
            .Range(0, 64 * 32)
            .Select(i => (byte)((i % 64 < 32 ? 0 : 128) + (i % 32)))
            .ToArray();
        var plane = Enumerable.Repeat((byte)1, pixels.Length).ToArray();
        var source = PresenceFrameFixture.Create(pixels, 64, 32);
        var mask = FingerprintProcessingMask.Create(64, 32, plane, plane).Value;
        var policy = FingerprintPreprocessingPolicy.Create(blend: 1, contrast: 0).Value;

        var pixelsOut = FingerprintPreprocessing
            .Process(source, mask, policy, CancellationToken.None)
            .Value.Pixels;

        // Independent tileCDFs are88/24 at31; center weight15.5/32 gives57.
        pixelsOut[31].Should().Be(57);
        // TileCDFs160/98 at128 interpolate at16.5/32, giving128 after rounding.
        pixelsOut[32].Should().Be(128);
        pixelsOut[0].Should().Be(3);
        pixelsOut[63].Should().Be(183);
    }

    [Test]
    public void StoredPolicyAndArtifactIdentityCanBeVerifiedWithoutImageWork()
    {
        var source = PresenceFrameFixture.Create([0, 64, 128, 255]);
        var mask = FingerprintProcessingMask.Create(2, 2, [1, 1, 1, 1], [1, 0, 0, 0]).Value;
        var artifact = FingerprintPreprocessing
            .Process(
                source,
                mask,
                FingerprintPreprocessingPolicy.Create().Value,
                CancellationToken.None
            )
            .Value;

        var identity = FingerprintPreprocessingIdentity
            .Create(
                artifact.Source,
                artifact.OriginalDigest,
                artifact.ProcessedDigest,
                mask.Digest,
                artifact.PolicyDigest,
                artifact.ProcessingRevision,
                artifact.Version
            )
            .Value;

        identity.Should().Be(artifact.Identity);
    }
}
