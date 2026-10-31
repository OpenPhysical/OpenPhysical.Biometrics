// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

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
        Assert.That(result.Status, Is.EqualTo(FacialPreparationStatus.Prepared));
        var bytes = Incits385Codec.Write(result.Record!);
        var parsed = Incits385Codec.Parse(bytes);
        Assert.Multiple(() =>
        {
            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Value!.Images[0].ImageInformation.Width, Is.EqualTo(480));
            Assert.That(parsed.Value.Images[0].ImageInformation.Height, Is.EqualTo(640));
            Assert.That(parsed.Value.Images[0].ImageInformation.ImageDataType, Is.EqualTo(1));
            Assert.That(pixels, Is.EqualTo(original));
            Assert.That(result.ProfileDiagnostics, Is.Empty);
        });
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
        Assert.That(result.Status, Is.EqualTo(FacialPreparationStatus.AcquisitionReviewRequired));
        Assert.That(result.Record, Is.Null);
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
        Assert.That(result.Status, Is.EqualTo(FacialPreparationStatus.ProfileRejected));
        Assert.That(result.Record, Is.Null);
    }

    [Test]
    public void InvalidDimensionsAndBuffersFailBeforeEncoding()
    {
        var service = new FacialRecordPreparer();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Prepare([], int.MaxValue, 640, Metadata)
        );
        Assert.Throws<ArgumentException>(() => service.Prepare(new byte[3], 480, 640, Metadata));
    }
}
