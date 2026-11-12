// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class FvcBaseline
{
    internal static Result<ImmutableArray<byte>, DesktopError> Mask(
        string folder,
        JsonElement descriptor,
        CaptureFrame frame
    ) =>
        EvaluationEdge
            .Try(
                () =>
                    Read(
                        Path.Combine(
                            folder,
                            "legacy",
                            descriptor.GetProperty("id").GetString() + ".mask.gz"
                        )
                    ),
                "Evaluation.Baseline",
                "The frozen old-analyzer mask is unavailable or incomplete."
            )
            .Bind(result => result)
            .Ensure(
                mask =>
                    mask.All(value => value <= 1)
                    && EvaluationDigest.Same(
                        EvaluationDigest.Of(mask.ToArray()),
                        descriptor.GetProperty("legacyMaskSha256").GetString()!
                    )
                    && EvaluationDigest.Same(
                        EvaluationDigest.Of(frame.Pixels.ToArray()),
                        descriptor.GetProperty("paddedOriginalSha256").GetString()!
                    ),
                new DesktopError(
                    "Evaluation.Baseline",
                    "The old analyzer mask or associated original differs from the frozen baseline."
                )
            );

    private static Result<ImmutableArray<byte>, DesktopError> Read(string path)
    {
        using var input = File.OpenRead(path);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        var pixels = new byte[800 * 750];
        gzip.ReadExactly(pixels);
        return gzip.ReadByte() == -1
            ? ImmutableArray.Create(pixels)
            : new DesktopError("Evaluation.Baseline", "The old-analyzer mask geometry is invalid.");
    }
}
