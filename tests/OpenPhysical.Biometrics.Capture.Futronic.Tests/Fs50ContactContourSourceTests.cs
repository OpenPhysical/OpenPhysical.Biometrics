// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ContactContourSourceTests
{
    [Test]
    public void SourceMismatchCannotReuseAnotherFramesContactGeometry()
    {
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var diagnostics = Fs50ContactAnalysis.Analyze(pixels).Value.Diagnostics;
        pixels[0] = (byte)(pixels[0] == 0 ? 1 : pixels[0] - 1);
        Fs50ContactContourRegion
            .Create(
                Fs50EvidenceFrameFixture.Create(pixels, 1),
                diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Error.Code.Should()
            .Be(Fs50ContactContourErrorCodes.SourceMismatch);
    }

    [Test]
    public void MetadataRestoreChecksPlanesAndCountsWithoutRerunningImageAnalysis()
    {
        var artifact = Create();
        var restored = Restore(artifact, artifact.Planes, artifact.Statistics).Value;
        restored.SourceDigest.Should().Be(artifact.SourceDigest);
        restored.ContactDigest.Should().Be(artifact.ContactDigest);
        restored.FeatureMask.Should().Equal(artifact.FeatureMask);
        restored.ContextMask.Should().Equal(artifact.ContextMask);
        restored.ToProcessingRegion(false).Value.Mask.Inner.Should().Equal(artifact.FeatureMask);
        var wrong = artifact.Planes with { FeatureMask = artifact.ContextMask };
        Restore(artifact, wrong, artifact.Statistics)
            .Error.Code.Should()
            .Be(CaptureErrorCodes.InvalidFrame);
        Restore(artifact, artifact.Planes, artifact.Statistics with { FilledHoleArea = -1 })
            .Error.Code.Should()
            .Be(CaptureErrorCodes.InvalidFrame);
    }

    [Test]
    public void ContextEditsPreserveTheExactOriginalFeatureAndEvidenceIdentity()
    {
        var artifact = Create();
        var edited = artifact
            .WithContext(
                Fs50ContactContourPolicy
                    .Create(new() { ContextMargin = 0, ModifiedBoundaryGuard = 0 })
                    .Value,
                CancellationToken.None
            )
            .Value;
        edited.SourceDigest.Should().Be(artifact.SourceDigest);
        edited.EvidencePolicyDigest.Should().Be(artifact.EvidencePolicyDigest);
        edited.ContactMask.Should().Equal(artifact.ContactMask);
        edited.FilledHoleMask.Should().Equal(artifact.FilledHoleMask);
        edited.FeatureMask.Should().Equal(artifact.FeatureMask);
        edited.SupportMask.Should().Equal(artifact.SupportMask);
        edited.Statistics.ContextArea.Should().BeLessThan(artifact.Statistics.ContextArea);
        edited.ToProcessingRegion(true).Value.AppliedGuard.Should().Be(0);
    }

    private static Fs50ContactContourArtifact Create()
    {
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var original = pixels.ToArray();
        var diagnostics = Fs50ContactAnalysis.Analyze(pixels).Value.Diagnostics;
        var artifact = Fs50ContactContourRegion
            .Create(
                Fs50EvidenceFrameFixture.Create(pixels, 1),
                diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        pixels.Should().Equal(original);
        artifact.ContactMask.Should().Equal(diagnostics.ContactMask);
        artifact.SourceDigest.Should().Be(diagnostics.SourceDigest);
        return artifact;
    }

    private static CSharpFunctionalExtensions.Result<
        Fs50ContactContourArtifact,
        CaptureError
    > Restore(
        Fs50ContactContourArtifact artifact,
        Fs50ContactContourPlanes planes,
        Fs50ContactContourStatistics statistics
    ) =>
        Fs50ContactContourArtifact.Restore(
            artifact.SourceDigest,
            artifact.EvidenceVersion,
            artifact.EvidencePolicyDigest,
            artifact.Policy,
            planes,
            statistics,
            CancellationToken.None
        );
}
