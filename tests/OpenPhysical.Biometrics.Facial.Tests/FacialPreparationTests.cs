// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Faces;

internal sealed class FacialPreparationTests
{
    private static readonly FacialAcquisitionMetadata Metadata = new(
        new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero),
        1,
        2,
        true,
        true
    );

    [Test]
    public void ConfirmedCapturePreservesGeometryAndInputsInTheEncodedRecord()
    {
        var pixels = new byte[480 * 640 * 3];
        new Random(3207).NextBytes(pixels);
        var original = pixels.ToArray();
        var prepared = TestFixtures.NewRecordPreparer().Prepare(pixels, 480, 640, Metadata);
        prepared.Should().Succeed();
        var result = prepared.Value;
        result.Status.Should().Be(FacialPreparationStatus.Prepared);
        var bytes = Incits385Codec.Write(result.Record.Value).Value;
        var parsed = Incits385Codec.Parse(bytes);
        using (new AssertionScope())
        {
            parsed.IsSuccess.Should().BeTrue();
            parsed.Value.Record.Images[0].ImageInformation.Width.Should().Be(480);
            parsed.Value.Record.Images[0].ImageInformation.Height.Should().Be(640);
            parsed.Value.Record.Images[0].ImageInformation.ImageDataType.Should().Be(1);
            pixels.Should().Equal(original);
            result.ProfileDiagnostics.Should().BeEmpty();
        }
    }

    [Test]
    public void MissingAcquisitionEvidenceProducesAnOperatorReviewOutcome()
    {
        var prepared = TestFixtures
            .NewRecordPreparer()
            .Prepare(
                new byte[480 * 640 * 3],
                480,
                640,
                Metadata with
                {
                    FullFrontalConfirmed = false,
                }
            );
        prepared.Should().Succeed();
        prepared.Value.Status.Should().Be(FacialPreparationStatus.AcquisitionReviewRequired);
        prepared.Value.Record.HasValue.Should().BeFalse();
    }

    [Test]
    public void UnsupportedProfileGeometryProducesAnExplicitOutcome()
    {
        var prepared = TestFixtures
            .NewRecordPreparer()
            .Prepare(new byte[420 * 560 * 3], 420, 560, Metadata);
        prepared.Should().Succeed();
        prepared.Value.Status.Should().Be(FacialPreparationStatus.ProfileRejected);
        prepared.Value.Record.HasValue.Should().BeFalse();
    }

    [Test]
    public void OverCompressedImageProducesACompressionOutcome()
    {
        var prepared = TestFixtures
            .NewRecordPreparer()
            .Prepare(new byte[480 * 640 * 3], 480, 640, Metadata);
        prepared.Should().Succeed();
        prepared.Value.Status.Should().Be(FacialPreparationStatus.CompressionRejected);
    }

    [Test]
    public void InvalidDimensionsAndBuffersFailBeforeEncoding()
    {
        var service = TestFixtures.NewRecordPreparer();
        service
            .Prepare([], int.MaxValue, 640, Metadata)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidDimensions);
        service
            .Prepare(new byte[3], 480, 640, Metadata)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidPixelBuffer);
    }
}
