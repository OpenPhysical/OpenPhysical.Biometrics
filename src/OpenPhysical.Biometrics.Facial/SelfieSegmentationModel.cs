// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.ML.OnnxRuntime;

internal sealed class SelfieSegmentationModel : IDisposable
{
    internal const string Sha256 =
        "60460d8359b0fd476381126518b9fa6ced2d7aefcdc8634ed78a33a3b2989b93";
    private readonly InferenceSession session;

    private SelfieSegmentationModel(InferenceSession session) => this.session = session;

    internal static Result<SelfieSegmentationModel, FacialError> Create() =>
        FacialEdge.TryBind(
            () =>
            {
                using var options = new SessionOptions
                {
                    IntraOpNumThreads = 1,
                    InterOpNumThreads = 1,
                };
                return MediaPipeModelLoader
                    .OpenSession("selfie_segmenter_square.onnx", Sha256, options)
                    .Map(opened => new SelfieSegmentationModel(opened));
            },
            FacialErrorCodes.ModelLoadFailed,
            "The packaged person segmentation model could not be loaded."
        );

    internal Result<PortraitPersonMask, FacialError> Segment(PortraitImage image) =>
        FacialEdge
            .Try(
                () =>
                {
                    using var outputs = session.Run([
                        NamedOnnxValue.CreateFromTensor(
                            "input_1",
                            SelfieSegmentationTensor.Create(image)
                        ),
                    ]);
                    return outputs.Single().AsTensor<float>().ToArray();
                },
                FacialErrorCodes.ModelExecutionFailed,
                "The person segmentation model failed."
            )
            .Ensure(
                values =>
                    values.Length == 256 * 256
                    && values.All(value =>
                        float.IsFinite(value) && value >= -1e-6f && value <= 1.000001f
                    ),
                new FacialError(
                    FacialErrorCodes.SegmentationOutput,
                    "The segmentation model returned invalid probabilities."
                )
            )
            .Map(values => new PortraitPersonMask(
                image,
                values.Select(value => Math.Clamp(value, 0, 1)).ToImmutableArray()
            ));

    public void Dispose() => session.Dispose();
}
