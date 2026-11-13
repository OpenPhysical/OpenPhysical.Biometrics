// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Faces;

internal sealed class PortraitReviewedGeometryTests
{
    private static readonly FacialAcquisitionMetadata Acquisition = new(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        0,
        2,
        true,
        true,
        80
    );

    private static ReviewedPortraitGeometry Review(string sourceSha256) =>
        new(
            1,
            sourceSha256,
            808,
            1024,
            0,
            new PortraitPoint(416, 160),
            default,
            new PortraitPoint(270, 423),
            new PortraitPoint(615, 396)
        );

    [Test]
    public void ReviewedGeometryAndExplicitUnsignedOverrideRetainFailuresAndCreateACanonicalRecord()
    {
        var source = TestFixtures.Bytes("bush.jpg");
        using var service = TestFixtures.NewPreparer();
        var image = PortraitImage.Decode(source).Value;
        var analysis = service.Analyze(image);
        analysis.Should().Succeed();
        var initial = service.Prepare(analysis.Value);
        initial.Should().Succeed();
        initial.Value.Measurements.Value.ReviewedHeadBoundary.Should().BeFalse();
        // Fixed fixture annotations exercise operator input separately from inference.
        var result = service.Prepare(analysis.Value, Review(initial.Value.SourceSha256));
        result.Should().Succeed();
        result.Value.Status.Should().Be(PortraitStatus.ReviewRequired);
        result.Value.Measurements.Value.ReviewedHeadBoundary.Should().BeTrue();
        result.Value.Faces[0].Should().BeSameAs(analysis.Value.Faces[0]);
        service.Prepare(analysis.Value).Value.RgbPixels.Should().Equal(initial.Value.RgbPixels);
        var preparer = TestFixtures.NewRecordPreparer();
        preparer.Prepare(result.Value, Acquisition).Value.Record.HasNoValue.Should().BeTrue();
        var acknowledgement = FacialCaptureOverride
            .Create(result.Value, DateTimeOffset.UnixEpoch)
            .Value;
        var record = preparer.Prepare(
            result.Value,
            Acquisition,
            FacialPreparationPolicy.UnsignedStaging with
            {
                CaptureOverride = acknowledgement,
            }
        );
        record.Should().Succeed();
        record.Value.CaptureOverride.Value.Should().BeSameAs(acknowledgement);
        result.Value.Findings.Should().Contain(f => f.Code == "MouthOpen");
        var bytes = Incits385Codec.Write(record.Value.Record.Value).Value;
        Incits385Codec.Parse(bytes).IsSuccess.Should().BeTrue();
        (record.Value.Record.Value.Images[0].ImageData.Length * 15L)
            .Should()
            .BeGreaterThanOrEqualTo(480 * 640 * 3);
    }

    [Test]
    public void MismatchedOrMalformedReviewedGeometryFailsWithStableCodes()
    {
        var source = TestFixtures.Bytes("bush.jpg");
        using var service = TestFixtures.NewPreparer();
        var hash = service.Prepare(source).Value.SourceSha256;
        var review = Review(hash);
        service
            .Prepare(source, Review("wrong"))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.GeometryMismatch);
        service.Prepare(source, review with { Width = 1 }).Should().Fail();
        service.Prepare(source, review with { Version = 2 }).Should().Fail();
        service
            .Prepare(source, Maybe.From(review), Maybe.From(1))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.GeometryMismatch);
        service
            .Prepare(source, review with { Crown = new PortraitPoint(double.NaN, 1) })
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.GeometryPointInvalid);
    }
}
