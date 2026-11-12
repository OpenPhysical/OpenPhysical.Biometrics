// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50FrameContactAnalysisTests
{
    [Test]
    public void FrameAnalysisUsesItsOwnedPixelsAndPreservesSpanEvidenceAndPolicy()
    {
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var expected = Fs50ContactAnalysis.Analyze(pixels).Value;
        var frame = Fs50EvidenceFrameFixture.Create(pixels, 1);
        Array.Fill(pixels, (byte)255);
        var actual = Fs50ContactAnalysis.Analyze(frame).Value;
        actual.Diagnostics.SourceDigest.Should().Be(frame.PixelDigest);
        actual.Diagnostics.SourceDigest.Should().Be(expected.Diagnostics.SourceDigest);
        actual.ContactMask.Should().Equal(expected.ContactMask);
        actual.Diagnostics.CandidateMask.Should().Equal(expected.Diagnostics.CandidateMask);
        actual.Diagnostics.RidgeSupportMask.Should().Equal(expected.Diagnostics.RidgeSupportMask);
        actual.Mask.Should().Equal(expected.Mask);
        actual.Diagnostics.InnerMask.Should().Equal(expected.Diagnostics.InnerMask);
        var strict = Fs50EvidencePolicy.Create(new() { MinimumSignal = 255 }).Value;
        var rejected = Fs50ContactAnalysis.Analyze(frame, strict).Value;
        rejected.Issue.Should().Be(Fs50ContactIssue.TooLight);
        rejected.Diagnostics.PolicyDigest.Should().Be(strict.Digest);
    }

    [TestCase(false, 750)]
    [TestCase(true, 749)]
    public void FrameGeometryAndCorrectionStageAreCheckedBeforeImageAnalysis(
        bool corrected,
        int height
    )
    {
        var source = Fs50EvidenceFrameFixture
            .Create(Fs50EvidenceFrameFixture.Empty(), 1)
            .Provenance;
        var output = CaptureGeometry.Create(800, height).Value;
        var provenance = CaptureProvenance
            .Create(
                source.Device,
                source.DriverVersion,
                corrected ? source.RawGeometry : output,
                output,
                CaptureSettings.Create(200, false, corrected, TimeSpan.FromMinutes(5)).Value,
                corrected ? source.CalibrationDigest.AsSpan() : [],
                corrected ? source.CorrectionVersion : string.Empty,
                source.Resolution,
                source.Qualification
            )
            .Value;
        var frame = CaptureFrame
            .Create(new byte[800 * height], 1, DateTimeOffset.UnixEpoch, provenance)
            .Value;
        Fs50ContactAnalysis
            .Analyze(frame)
            .Error.Code.Should()
            .Be(CaptureErrorCodes.UnsupportedGeometry);
    }
}
