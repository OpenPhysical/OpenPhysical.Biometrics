// SPDX-License-Identifier: Apache-2.0
// Adapted from yakhyo/mediapipe-face-mesh-onnx, copyright 2026 Yakhyokhuja Valikhujaev.
// Changes: C# implementation, immutable outputs, bounded buffers, and deterministic ownership.
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Onnx;

// Reads packaged ONNX models and verifies their pinned checksums before use.
internal static class MediaPipeModelLoader
{
    internal static Result<byte[], FacialError> Load(string file, string hash) =>
        Maybe
            .From(
                typeof(MediaPipeModels).Assembly.GetManifestResourceStream(
                    "OpenPhysical.Biometrics.Onnx.Resources." + file
                )
            )
            .ToResult(FacialErrors.ModelMissing())
            .Bind(ReadAll)
            .Ensure(
                bytes => Sha256.HexDigest(bytes).Equals(hash, StringComparison.OrdinalIgnoreCase),
                FacialErrors.ModelChecksum()
            );

    private static Result<byte[], FacialError> ReadAll(Stream resource) =>
        FacialEdge.Try(
            () =>
            {
                using var stream = resource;
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            },
            FacialErrorCodes.ModelLoadFailed,
            "The packaged MediaPipe model could not be read."
        );
}
