// SPDX-License-Identifier: Apache-2.0
// Adapted from yakhyo/mediapipe-face-mesh-onnx, copyright 2026 Yakhyokhuja Valikhujaev.
// Changes: C# implementation, immutable outputs, bounded buffers, and deterministic ownership.
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

internal sealed class MediaPipeDetector : IDisposable
{
    private readonly InferenceSession detector;
    private readonly InferenceSession mesh;

    private MediaPipeDetector(InferenceSession detector, InferenceSession mesh)
    {
        this.detector = detector;
        this.mesh = mesh;
    }

    internal static Result<MediaPipeDetector, FacialError> Create() =>
        FacialEdge.TryBind(
            OpenSessions,
            FacialErrorCodes.ModelLoadFailed,
            "The packaged MediaPipe models could not be loaded."
        );

    internal Result<ImmutableArray<PortraitFace>, FacialError> Detect(PortraitPixels image)
    {
        var box = DetectorBox.For(image);
        return image
            .Warp(DetectorBox.Size, DetectorBox.Size, box.Transform, true)
            .Bind(RunDetector)
            .Bind(BlazeFaceDecoder.Suppress)
            .Bind(rows => Locate(image, box, rows))
            .Map(faces =>
                faces
                    .OrderBy(f => f.X)
                    .ThenBy(f => f.Y)
                    .Select((f, i) => f.Face with { Index = i })
                    .ToImmutableArray()
            );
    }

    public void Dispose()
    {
        mesh.Dispose();
        detector.Dispose();
    }

    private static Result<MediaPipeDetector, FacialError> OpenSessions()
    {
        using var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        return OpenSession(
                "face_detection_short_range.onnx",
                "2f2689b040becf555706d2cb978d2f0e3296ea82413734fba9a856c66c5f2b17",
                options
            )
            .Bind(opened =>
                OpenSession(
                        "face_landmarker_Nx3x256x256.onnx",
                        "111795f8703cdeb6d0c68a9f3cc966a0f23f8786bb00f4577a11f461fc4276ac",
                        options
                    )
                    .TapError(_ => opened.Dispose())
                    .Map(second => new MediaPipeDetector(opened, second))
            );
    }

    private static Result<InferenceSession, FacialError> OpenSession(
        string file,
        string hash,
        SessionOptions options
    ) =>
        MediaPipeModelLoader
            .Load(file, hash)
            .Bind(bytes =>
                FacialEdge.Try(
                    () => new InferenceSession(bytes, options),
                    FacialErrorCodes.ModelLoadFailed,
                    "The packaged MediaPipe model could not be loaded."
                )
            );

    private Result<List<double[]>, FacialError> RunDetector(byte[] pixels) =>
        FacialEdge
            .Try(
                () =>
                {
                    using var outputs = detector.Run([
                        NamedOnnxValue.CreateFromTensor(
                            "input",
                            MediaPipeTensor.Create(pixels, 128, true)
                        ),
                    ]);
                    return (
                        Raw: outputs.First().AsTensor<float>().ToArray(),
                        Logits: outputs.Last().AsTensor<float>().ToArray()
                    );
                },
                FacialErrorCodes.ModelExecutionFailed,
                "The face detector failed."
            )
            .Bind(output =>
                output.Raw.Length != BlazeFaceDecoder.Anchors * BlazeFaceDecoder.Values
                || output.Logits.Length != BlazeFaceDecoder.Anchors
                    ? FacialErrors.DetectorOutput().Fail<List<double[]>>()
                    : BlazeFaceDecoder.Candidates(output.Raw, output.Logits).Succeed()
            );

    private Result<List<(double X, double Y, PortraitFace Face)>, FacialError> Locate(
        PortraitPixels image,
        DetectorBox box,
        List<double[]> rows
    )
    {
        var faces = new List<(double X, double Y, PortraitFace Face)>();
        foreach (var row in rows)
        {
            var located = Landmarks(image, box, row);
            if (located.IsFailure)
                return located.Error.Fail<List<(double X, double Y, PortraitFace Face)>>();
            if (located.Value.HasValue)
                faces.Add(located.Value.Value);
        }
        return faces.Succeed();
    }

    private Result<Maybe<(double X, double Y, PortraitFace Face)>, FacialError> Landmarks(
        PortraitPixels image,
        DetectorBox box,
        double[] row
    )
    {
        var cx = box.SourceX(row[1]);
        var cy = box.SourceY(row[2]);
        var side = Math.Max(row[3], row[4]) * DetectorBox.Size / box.Scale * 1.5;
        var angle = Math.Atan2(row[8] - row[6], row[7] - row[5]);
        var a = Math.Cos(angle) * 256 / side;
        var b = Math.Sin(angle) * 256 / side;
        var transform = new PortraitTransform(a, b, 128 - a * cx - b * cy, 128 + b * cx - a * cy);
        return image
            .Warp(256, 256, transform, true)
            .Bind(RunMesh)
            .Map(output => MeshFace(output, row, box, transform, side, (cx, cy)));
    }

    private Result<MeshOutput, FacialError> RunMesh(byte[] pixels) =>
        FacialEdge
            .Try(
                () =>
                {
                    using var result = mesh.Run([
                        NamedOnnxValue.CreateFromTensor(
                            "input",
                            MediaPipeTensor.Create(pixels, 256, false)
                        ),
                    ]);
                    return new MeshOutput(
                        result.First(o => o.Name == "landmarks").AsTensor<float>().ToArray(),
                        BlazeFaceDecoder.Sigmoid(
                            result.First(o => o.Name == "score").AsTensor<float>().First()
                        )
                    );
                },
                FacialErrorCodes.ModelExecutionFailed,
                "The landmark network failed."
            )
            .Ensure(
                output =>
                    output.Values.Length == 1434
                    && output.Values.All(float.IsFinite)
                    && double.IsFinite(output.Presence),
                FacialErrors.LandmarkOutput()
            );

    private static Maybe<(double X, double Y, PortraitFace Face)> MeshFace(
        MeshOutput output,
        double[] row,
        DetectorBox box,
        PortraitTransform transform,
        double side,
        (double X, double Y) center
    )
    {
        if (output.Presence < .5)
            return Maybe<(double X, double Y, PortraitFace Face)>.None;
        var values = output.Values;
        var points = Enumerable
            .Range(0, 478)
            .Select(i =>
            {
                var point = transform.InverseUnchecked(new(values[i * 3], values[i * 3 + 1]));
                return new FaceMeshPoint(point.X, point.Y, values[i * 3 + 2] * side / 256);
            })
            .ToImmutableArray();
        PortraitPoint Ear(int k) => new(box.SourceX(row[5 + k * 2]), box.SourceY(row[6 + k * 2]));
        return (
            center.X,
            center.Y,
            new PortraitFace(0, row[0], output.Presence, points, Ear(4), Ear(5))
        );
    }
}
