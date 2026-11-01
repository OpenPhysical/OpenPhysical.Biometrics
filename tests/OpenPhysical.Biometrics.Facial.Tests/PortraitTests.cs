// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NUnit.Framework;

internal sealed class PortraitTests
{
    [Test]
    public void PublishedNetworkMatchesRecordedPythonTensor()
    {
        var folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures");
        static float[] Floats(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        using var session = new InferenceSession(
            MediaPipeDetector.Load(
                "face_landmarker_Nx3x256x256.onnx",
                "111795f8703cdeb6d0c68a9f3cc966a0f23f8786bb00f4577a11f461fc4276ac"
            )
        );
        using var output = session.Run([
            NamedOnnxValue.CreateFromTensor(
                "input",
                new DenseTensor<float>(
                    Floats(Path.Combine(folder, "glenn.input.f32")),
                    [1, 3, 256, 256]
                )
            ),
        ]);
        var actual = output.First(o => o.Name == "landmarks").AsTensor<float>().ToArray();
        var expected = Floats(Path.Combine(folder, "glenn.output0.f32"));
        actual.Should().HaveCount(1434);
        actual.Zip(expected, (a, b) => Math.Abs(a - b)).Max().Should().BeLessThan(.002f);
    }

    [Test]
    public void FullImageGeometryTracksThePinnedPythonReference()
    {
        var folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures");
        using var reference = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(folder, "glenn.reference.json"))
        );
        using var service = new PortraitPreparer();
        var actual = service
            .Prepare(File.ReadAllBytes(Path.Combine(folder, "glenn.jpg")))
            .Faces.Single();
        var expected = reference.RootElement.GetProperty("landmarks").EnumerateArray().ToArray();
        var differences = actual
            .Landmarks.Select(
                (point, i) =>
                    Math.Sqrt(
                        Math.Pow(point.X - expected[i][0].GetDouble(), 2)
                            + Math.Pow(point.Y - expected[i][1].GetDouble(), 2)
                    )
            )
            .ToArray();
        // Skia and OpenCV decode/resample independently; this bound is on source pixels.
        differences.Max().Should().BeLessThan(2);
    }

    [Test]
    public void PortraitDetectionOwnsItsDataAndRepeatedCallsAgree()
    {
        var source = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "glenn.jpg")
        );
        var original = source.ToArray();
        using var service = new PortraitPreparer();
        var first = service.Prepare(source);
        first.Faces.Should().HaveCount(1);
        first.Faces[0].Landmarks.Should().HaveCount(478);
        Parallel.For(
            0,
            4,
            _ => service.Prepare(source).Faces[0].Landmarks.Should().Equal(first.Faces[0].Landmarks)
        );
        source.Should().Equal(original);
        FluentActions
            .Invoking(() =>
                service.Prepare(source, new(1, "wrong", first.SourceWidth, first.SourceHeight, 0))
            )
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Test]
    public void BlankAndMalformedInputsHaveExplicitOutcomes()
    {
        using var service = new PortraitPreparer();
        var png = PortraitEncoding.EncodePng(new byte[480 * 640 * 3], 480, 640);
        service.Prepare(png).Status.Should().Be(PortraitStatus.NoFace);
        FluentActions
            .Invoking(() => service.Prepare([1, 2, 3]))
            .Should()
            .ThrowExactly<ArgumentException>();
        service.Dispose();
        FluentActions
            .Invoking(() => service.Prepare(png))
            .Should()
            .ThrowExactly<ObjectDisposedException>();
    }

    [Test]
    public void ReviewedGeometryProducesUniformSourceSupportedCrop()
    {
        var (face, review) = Geometry();
        var result = PortraitCrop.Calculate(face, review, 1200, 1600);
        result.Transform.Should().NotBeNull();
        using (new AssertionScope())
        {
            result.Measurements!.HeadWidth.Should().BeApproximately(256, .001);
            result.Measurements.EyeHeightFraction.Should().BeApproximately(.4, .001);
            result.Measurements.HeadHeight.Should().BeLessThanOrEqualTo(512);
            result.Measurements.ReviewedHeadBoundary.Should().BeTrue();
        }
        var point = new PortraitPoint(532, 731);
        var restored = result.Transform!.Inverse(result.Transform.Map(point));
        restored.X.Should().BeApproximately(point.X, 1e-8);
        restored.Y.Should().BeApproximately(point.Y, 1e-8);
    }

    [Test]
    public void SmallHeadsAndImpossibleSourceCoverageRequireReview()
    {
        var (face, review) = Geometry();
        PortraitCrop
            .Calculate(face, review with { ViewerRightEar = new(630, 650) }, 1200, 1600)
            .Transform.Should()
            .BeNull();
        PortraitCrop.Calculate(face, review, 650, 900).Transform.Should().BeNull();
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void ExifOrientationDefinesTheReviewedCoordinateSpace(int orientation)
    {
        using var bitmap = new SkiaSharp.SKBitmap(8, 5);
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 8; x++)
            bitmap.SetPixel(x, y, new SkiaSharp.SKColor((byte)(x * 30), (byte)(y * 50), 80));
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 95);
        var jpeg = data.ToArray();
        var plain = PortraitPixels.Decode(jpeg);
        byte[] exif =
        [
            0xff,
            0xe1,
            0,
            34,
            69,
            120,
            105,
            102,
            0,
            0,
            73,
            73,
            42,
            0,
            8,
            0,
            0,
            0,
            1,
            0,
            18,
            1,
            3,
            0,
            1,
            0,
            0,
            0,
            (byte)orientation,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
        ];
        var encoded = jpeg.Take(2).Concat(exif).Concat(jpeg.Skip(2)).ToArray();
        var actual = PortraitPixels.Decode(encoded);
        actual.Width.Should().Be(orientation >= 5 ? 5 : 8);
        actual.Height.Should().Be(orientation >= 5 ? 8 : 5);
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 8; x++)
        {
            var (dx, dy) = orientation switch
            {
                2 => (7 - x, y),
                3 => (7 - x, 4 - y),
                4 => (x, 4 - y),
                5 => (y, x),
                6 => (4 - y, x),
                7 => (4 - y, 7 - x),
                8 => (y, 7 - x),
                _ => (x, y),
            };
            for (var c = 0; c < 3; c++)
                actual
                    .Rgb[(dy * actual.Width + dx) * 3 + c]
                    .Should()
                    .Be(plain.Rgb[(y * 8 + x) * 3 + c]);
        }
    }

    [Test]
    public void MissingAndCorruptModelResourcesFailExplicitly()
    {
        FluentActions
            .Invoking(() => MediaPipeDetector.Load("absent.onnx", "00"))
            .Should()
            .ThrowExactly<InvalidOperationException>();
        FluentActions
            .Invoking(() => MediaPipeDetector.Load("face_detection_short_range.onnx", "00"))
            .Should()
            .ThrowExactly<InvalidOperationException>();
    }

    [Test]
    public void RollCorrectionPreservesTheReviewedMeasurements()
    {
        var (face, review) = Geometry();
        var rotate = new PortraitTransform(Math.Cos(.15), Math.Sin(.15), 40, 100);
        PortraitPoint? Map(PortraitPoint? p) => p is { } point ? rotate.Map(point) : null;
        var tilted = face with
        {
            Landmarks = face
                .Landmarks.Select(p =>
                {
                    var q = rotate.Map(new(p.X, p.Y));
                    return new FaceMeshPoint(q.X, q.Y, p.Z);
                })
                .ToImmutableArray(),
            ViewerLeftEar = rotate.Map(face.ViewerLeftEar),
            ViewerRightEar = rotate.Map(face.ViewerRightEar),
        };
        var result = PortraitCrop.Calculate(
            tilted,
            review with
            {
                Crown = Map(review.Crown),
                ViewerLeftEar = Map(review.ViewerLeftEar),
                ViewerRightEar = Map(review.ViewerRightEar),
            },
            1600,
            1800
        );
        result.Transform.Should().NotBeNull();
        result.Measurements!.HeadWidth.Should().BeApproximately(256, .001);
    }

    [Test]
    public void NoiseIsRejectedAndMultipleFacesRequireSelection()
    {
        using var service = new PortraitPreparer();
        var noise = new byte[480 * 640 * 3];
        new Random(1976).NextBytes(noise);
        service
            .Prepare(PortraitEncoding.EncodePng(noise, 480, 640))
            .Status.Should()
            .Be(PortraitStatus.NoFace);
        using var portrait = SkiaSharp.SKBitmap.Decode(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "bush.jpg")
        );
        using var pair = new SkiaSharp.SKBitmap(portrait.Width * 2, portrait.Height);
        using (var canvas = new SkiaSharp.SKCanvas(pair))
        {
            canvas.DrawBitmap(portrait, 0, 0);
            canvas.DrawBitmap(portrait, portrait.Width, 0);
        }
        using var image = SkiaSharp.SKImage.FromBitmap(pair);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var result = service.Prepare(encoded.ToArray());
        result.Status.Should().Be(PortraitStatus.FaceSelectionRequired);
        result.Faces.Should().HaveCount(2);
        result
            .Faces[0]
            .Features.NoseBridge.X.Should()
            .BeLessThan(result.Faces[1].Features.NoseBridge.X);
        service
            .Prepare(encoded.ToArray(), faceIndex: 1)
            .Status.Should()
            .NotBe(PortraitStatus.FaceSelectionRequired);
        FluentActions
            .Invoking(() => service.Prepare(encoded.ToArray(), faceIndex: 2))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public void RealPortraitEncodesAndReviewedGeometryCreatesACanonicalRecord()
    {
        var source = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "bush.jpg")
        );
        using var service = new PortraitPreparer();
        var initial = service.Prepare(source);
        initial.Status.Should().Be(PortraitStatus.Prepared);
        initial.Measurements!.ReviewedHeadBoundary.Should().BeFalse();
        // Fixed fixture annotations exercise operator input separately from inference.
        var review = new ReviewedPortraitGeometry(
            1,
            initial.SourceSha256,
            808,
            1024,
            0,
            new(416, 160),
            null,
            new(270, 423),
            new(615, 396)
        );
        var result = service.Prepare(source, review);
        result.Status.Should().Be(PortraitStatus.Prepared);
        result.Measurements!.ReviewedHeadBoundary.Should().BeTrue();
        var acquisition = new FacialAcquisitionMetadata(
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            0,
            2,
            true,
            true,
            80
        );
        var record = new FacialRecordPreparer().Prepare(
            result.RgbPixels.AsSpan(),
            480,
            640,
            acquisition
        );
        record.Status.Should().Be(FacialPreparationStatus.Prepared);
        var bytes = OpenPhysical
            .Biometrics.Formats.Faces.Incits385Codec.Write(record.Record!)
            .Value;
        var parsed = OpenPhysical.Biometrics.Formats.Faces.Incits385Codec.Parse(bytes);
        parsed.IsSuccess.Should().BeTrue();
        (record.Record!.Images[0].ImageData.Length * 15L)
            .Should()
            .BeGreaterThanOrEqualTo(480 * 640 * 3);
        FluentActions
            .Invoking(() => service.Prepare(source, review with { Crown = new(double.NaN, 1) }))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() => service.Prepare(source, review with { Width = 1 }))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() => service.Prepare(source, review with { Version = 2 }))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Test]
    public void CatControlProducesNoFace()
    {
        using var service = new PortraitPreparer();
        var image = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "maddie.jpeg")
        );
        service.Prepare(image).Status.Should().Be(PortraitStatus.NoFace);
    }

    [Test]
    public void EmbeddedLinearColorProfileIsConvertedToSrgb()
    {
        using var linear = SkiaSharp.SKColorSpace.CreateSrgbLinear();
        using var bitmap = new SkiaSharp.SKBitmap(
            new SkiaSharp.SKImageInfo(
                8,
                8,
                SkiaSharp.SKColorType.Rgba8888,
                SkiaSharp.SKAlphaType.Opaque,
                linear
            )
        );
        var samples = bitmap.GetPixelSpan();
        for (var i = 0; i < 64; i++)
        {
            samples[i * 4] = samples[i * 4 + 1] = samples[i * 4 + 2] = 128;
            samples[i * 4 + 3] = 255;
        }
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var decoded = PortraitPixels.Decode(encoded.ToArray());
        // Half-intensity linear light maps to about 188 in 8-bit sRGB.
        decoded.Rgb[0].Should().BeInRange(186, 190);
    }

    [Test]
    public void IndependentGeometryRejectsOffCenterFeaturesAndExcessiveHeadHeight()
    {
        var (face, review) = Geometry();
        var points = face.Landmarks.ToArray();
        points[13] = points[13] with { X = 800 };
        points[14] = points[14] with { X = 800 };
        PortraitCrop
            .Calculate(face with { Landmarks = points.ToImmutableArray() }, review, 1200, 1600)
            .Transform.Should()
            .BeNull();
        PortraitCrop
            .Calculate(face, review with { Crown = new(600, 0) }, 1200, 1600)
            .Transform.Should()
            .BeNull();
        var result = PortraitCrop.Calculate(face, review with { ViewerLeftEar = null }, 1200, 1600);
        result.Measurements!.CrownOrigin.Should().Be(PortraitMeasurementOrigin.Reviewed);
        result.Measurements.ViewerLeftEarOrigin.Should().Be(PortraitMeasurementOrigin.Estimated);
        result.Measurements.ChinOrigin.Should().Be(PortraitMeasurementOrigin.Mesh);
    }

    private static (PortraitFace Face, ReviewedPortraitGeometry Review) Geometry()
    {
        var points = Enumerable.Repeat(new FaceMeshPoint(600, 700, 0), 478).ToArray();
        points[33] = new(510, 650, 0);
        points[133] = new(550, 650, 0);
        points[362] = new(650, 650, 0);
        points[263] = new(690, 650, 0);
        points[152] = new(600, 950, 0);
        points[168] = new(600, 670, 0);
        points[13] = new(600, 800, 0);
        points[14] = new(600, 810, 0);
        return (
            new(0, .99, .99, points.ToImmutableArray(), new(440, 650), new(760, 650)),
            new(1, "fixture", 1200, 1600, 0, new(600, 400), null, new(440, 650), new(760, 650))
        );
    }
}
