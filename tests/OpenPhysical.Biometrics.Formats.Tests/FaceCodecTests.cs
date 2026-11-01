// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using System.Buffers.Binary;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Faces;

/// <summary>Exercises INCITS 385 wire layouts and image-block boundaries.</summary>
[TestFixture]
public sealed class FaceCodecTests
{
    /// <summary>Maintains the full facial quality field and all facial image blocks.</summary>
    [Test]
    public void FaceImagesAndSixteenBitQualityRoundTrip()
    {
        var bytes = Incits385Codec.Write(CodecTestData.Face()).Value;
        var parsed = Incits385Codec.Parse(bytes);
        parsed.Should().Succeed();
        var record = parsed.Value.Record;
        using (new AssertionScope())
        {
            record.Images.Length.Should().Be(2);
            record.Images[0].ImageInformation.Quality.Should().Be(0xFFFF);
            record.Images[1].ImageData.Should().Equal(new byte[] { 1, 2, 3 });
            Incits385Codec.Write(record).Value.Should().Equal(bytes);
        }
    }

    /// <summary>Prevents a facial block from borrowing the next image's metadata.</summary>
    [Test]
    public void FacialFeatureCountIsBoundedByItsOwnImageBlock()
    {
        var bytes = Incits385Codec.Write(CodecTestData.Face()).Value;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(18), 10);
        Incits385Codec.Parse(bytes).Should().Fail().Which.Code.Should().Be("Truncated");
    }

    /// <summary>Reports unencodable facial fields as failed results with stable codes.</summary>
    [Test]
    public void WritingUnencodableFieldsFailsWithStableCodes()
    {
        var record = CodecTestData.Face();
        var image = record.Images[0];
        using (new AssertionScope())
        {
            Incits385Codec
                .Write(new FaceRecord([]))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("ImageCount");
            Incits385Codec
                .Write(new FaceRecord([image with { ImageData = [] }]))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("FaceBlock");
            Incits385Codec
                .Write(
                    new FaceRecord([
                        image with
                        {
                            Information = image.Information with { FeatureMask = 0x1000000 },
                        },
                    ])
                )
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("FaceBlock");
        }
    }
}
