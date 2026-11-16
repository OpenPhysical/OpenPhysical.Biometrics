// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class UnsignedFacialPreparationTests
{
    [Test]
    public void UnsignedStagingKeepsUnknownOpticalEvidenceWhileTheDefaultRemainsStrict()
    {
        var (portrait, _) = TestFixtures.PreparedPortrait();
        var metadata = new FacialAcquisitionMetadata(DateTimeOffset.UtcNow, 0, 6, true, false);
        var preparer = TestFixtures.NewRecordPreparer();
        var strict = preparer.Prepare(portrait, metadata);
        strict.Should().Succeed();
        strict.Value.Record.HasNoValue.Should().BeTrue();
        var acknowledgement = FacialCaptureOverride
            .Create(portrait, DateTimeOffset.UnixEpoch)
            .Value;
        var unsigned = preparer.Prepare(
            portrait,
            metadata,
            FacialPreparationPolicy.UnsignedStaging with
            {
                CaptureOverride = acknowledgement,
            }
        );
        unsigned.Should().Succeed();
        unsigned.Value.Status.Should().Be(FacialPreparationStatus.Prepared);
        unsigned.Value.Acquisition.CapturedAt.Should().Be(metadata.CapturedAt);
        unsigned.Value.CaptureOverride.Value.Should().BeSameAs(acknowledgement);
        unsigned.Value.Acquisition.OpticalResolutionConfirmed.Should().BeFalse();
        unsigned
            .Value.AcquisitionFindings.Should()
            .Contain(value => value.Code == "OpticalResolutionUnknown");
        unsigned.Value.ProfileDiagnostics.Should().BeEmpty();
        var unreviewed = preparer.Prepare(
            portrait,
            metadata,
            FacialPreparationPolicy.UnsignedStaging
        );
        unreviewed.Value.Record.HasNoValue.Should().BeTrue();
    }
}
