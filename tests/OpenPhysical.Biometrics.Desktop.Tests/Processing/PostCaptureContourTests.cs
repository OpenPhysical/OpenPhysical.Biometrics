// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

[TestFixture]
internal sealed class PostCaptureContourTests
{
    [TestCase(0, 0)]
    [TestCase(7, 8)]
    public void ContextAndFilledContactHaveSeparateProcessingRoles(int brightness, int guard)
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var request = new CaptureProcessingRequest(
            FingerprintPreprocessingPolicy.Create(false, brightness: brightness, contrast: 0).Value,
            Fs50EvidencePolicy.Default,
            Fs50ContactContourPolicy.Default,
            17,
            CaptureProcessingInput.DeclaredOfflineImage
        );
        var result = services.Processor.PrepareFrame(frame, request, default);
        result.Should().Succeed();
        var prepared = result.Value;
        prepared.Contour.SourceDigest.Should().Be(prepared.Artifact.OriginalDigest);
        prepared.Artifact.Mask.Retained.Should().Equal(prepared.Contour.ContextMask);
        prepared.ProcessingRegion.AppliedGuard.Should().Be(guard);
        prepared.ProcessingRegion.Mask.Should().BeSameAs(prepared.Artifact.Mask);
        prepared
            .Artifact.Mask.Inner.Should()
            .Equal(
                prepared.Contour.FeatureMask.Zip(
                    prepared.ProcessingRegion.GuardedContextMask,
                    (f, g) => (byte)(f & g)
                )
            );
        prepared
            .Contour.ContactMask.Zip(prepared.Contour.FeatureMask, (c, f) => c == 0 || f == 1)
            .Should()
            .OnlyContain(value => value);
        if (brightness == 0)
        {
            prepared.Artifact.Pixels.Should().Equal(frame.Pixels);
            prepared.Artifact.Mask.Inner.Should().Equal(prepared.Contour.FeatureMask);
        }
        services.Counts.Should().Be((0, 0, 0));
    }
}
