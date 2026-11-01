// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NUnit.Framework;

internal sealed class PortraitModelTests
{
    private const string MeshModel = "face_landmarker_Nx3x256x256.onnx";
    private const string MeshHash =
        "111795f8703cdeb6d0c68a9f3cc966a0f23f8786bb00f4577a11f461fc4276ac";

    [Test]
    public void PublishedNetworkMatchesRecordedPythonTensor()
    {
        static float[] Floats(string name)
        {
            var bytes = TestFixtures.Bytes(name);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        var model = MediaPipeModelLoader.Load(MeshModel, MeshHash);
        model.Should().Succeed();
        using var session = new InferenceSession(model.Value);
        using var output = session.Run([
            NamedOnnxValue.CreateFromTensor(
                "input",
                new DenseTensor<float>(Floats("glenn.input.f32"), [1, 3, 256, 256])
            ),
        ]);
        var actual = output.First(o => o.Name == "landmarks").AsTensor<float>().ToArray();
        var expected = Floats("glenn.output0.f32");
        actual.Should().HaveCount(1434);
        actual.Zip(expected, (a, b) => Math.Abs(a - b)).Max().Should().BeLessThan(.002f);
    }

    [Test]
    public void FullImageGeometryTracksThePinnedPythonReference()
    {
        using var reference = JsonDocument.Parse(TestFixtures.Bytes("glenn.reference.json"));
        using var service = TestFixtures.NewPreparer();
        var prepared = service.Prepare(TestFixtures.Bytes("glenn.jpg"));
        prepared.Should().Succeed();
        var actual = prepared.Value.Faces.Single();
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
    public void FaceFeaturesAreDerivedFromTheCanonicalMesh()
    {
        var (face, _) = TestFixtures.Geometry();
        var features = face.Features;
        features.HasValue.Should().BeTrue();
        features.Value.NoseBridge.Should().Be(new PortraitPoint(600, 670));
        (face with { Landmarks = face.Landmarks.RemoveAt(0) }).Features.HasValue.Should().BeFalse();
    }
}
