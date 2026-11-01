// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Faces;

internal sealed class PortraitReviewedGeometryTests
{
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
    public void RealPortraitEncodesAndReviewedGeometryCreatesACanonicalRecord()
    {
        var source = TestFixtures.Bytes("bush.jpg");
        using var service = TestFixtures.NewPreparer();
        var initial = service.Prepare(source);
        initial.Should().Succeed();
        initial.Value.Status.Should().Be(PortraitStatus.Prepared);
        initial.Value.Measurements.Value.ReviewedHeadBoundary.Should().BeFalse();
        // Fixed fixture annotations exercise operator input separately from inference.
        var result = service.Prepare(source, Review(initial.Value.SourceSha256));
        result.Should().Succeed();
        result.Value.Status.Should().Be(PortraitStatus.Prepared);
        result.Value.Measurements.Value.ReviewedHeadBoundary.Should().BeTrue();
        var acquisition = new FacialAcquisitionMetadata(
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            0,
            2,
            true,
            true,
            80
        );
        var record = TestFixtures
            .NewRecordPreparer()
            .Prepare(result.Value.RgbPixels.AsSpan(), 480, 640, acquisition);
        record.Should().Succeed();
        record.Value.Status.Should().Be(FacialPreparationStatus.Prepared);
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
