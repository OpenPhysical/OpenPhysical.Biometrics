// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
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
        var result = new FacialRecordPreparer().Prepare(pixels, 480, 640, Metadata);
        result.Status.Should().Be(FacialPreparationStatus.Prepared);
        var bytes = Incits385Codec.Write(result.Record!);
        var parsed = Incits385Codec.Parse(bytes);
        using (new AssertionScope())
        {
            parsed.IsSuccess.Should().BeTrue();
            parsed.Value!.Images[0].ImageInformation.Width.Should().Be(480);
            parsed.Value.Images[0].ImageInformation.Height.Should().Be(640);
            parsed.Value.Images[0].ImageInformation.ImageDataType.Should().Be(1);
            pixels.Should().Equal(original);
            result.ProfileDiagnostics.Should().BeEmpty();
        }
    }

    [Test]
    public void MissingAcquisitionEvidenceProducesAnOperatorReviewOutcome()
    {
        var result = new FacialRecordPreparer().Prepare(
            new byte[480 * 640 * 3],
            480,
            640,
            Metadata with
            {
                FullFrontalConfirmed = false,
            }
        );
        result.Status.Should().Be(FacialPreparationStatus.AcquisitionReviewRequired);
        result.Record.Should().BeNull();
    }

    [Test]
    public void UnsupportedProfileGeometryProducesAnExplicitOutcome()
    {
        var result = new FacialRecordPreparer().Prepare(
            new byte[420 * 560 * 3],
            420,
            560,
            Metadata
        );
        result.Status.Should().Be(FacialPreparationStatus.ProfileRejected);
        result.Record.Should().BeNull();
    }

    [Test]
    public void InvalidDimensionsAndBuffersFailBeforeEncoding()
    {
        var service = new FacialRecordPreparer();
        FluentActions
            .Invoking(() => service.Prepare([], int.MaxValue, 640, Metadata))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
        FluentActions
            .Invoking(() => service.Prepare(new byte[3], 480, 640, Metadata))
            .Should()
            .ThrowExactly<ArgumentException>();
    }
}
