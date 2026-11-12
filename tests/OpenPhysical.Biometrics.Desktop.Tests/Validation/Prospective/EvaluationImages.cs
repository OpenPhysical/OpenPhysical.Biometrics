// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class EvaluationImages
{
    internal static Result<EvaluationInput, DesktopError> Read(
        string folder,
        JsonElement descriptor
    )
    {
        var id = descriptor.GetProperty("id").GetString()!;
        var width = descriptor.GetProperty("width").GetInt32();
        var height = descriptor.GetProperty("height").GetInt32();
        if (!ValidId(id) || width is < 1 or > 800 || height is < 1 or > 750)
            return Invalid("The declared public image identifier or geometry is invalid.");
        var raw = File.ReadAllBytes(Path.Combine(folder, id + ".gray8"));
        if (
            raw.Length != width * height
            || !EvaluationDigest.Same(
                EvaluationDigest.Of(raw),
                descriptor.GetProperty("sha256").GetString()!
            )
        )
            return Invalid("A public image differs from its pinned manifest.");
        var frame = Pad(raw, width, height);
        return FvcBaseline
            .Mask(folder, descriptor, frame)
            .Bind(mask =>
                ProspectiveHistoricalContact
                    .Read(descriptor)
                    .Map(
                        (Maybe<DesktopError> rejection) =>
                            new EvaluationInput(
                                id,
                                descriptor.GetProperty("fingerId").ToString(),
                                frame,
                                mask,
                                rejection
                            )
                    )
            );
    }

    private static CaptureFrame Pad(byte[] raw, int width, int height)
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        var x = (800 - width) / 2;
        var y = (750 - height) / 2;
        for (var row = 0; row < height; row++)
            raw.AsSpan(row * width, width).CopyTo(pixels.AsSpan((row + y) * 800 + x, width));
        return ProcessingFrameFixtures.NormalizedFrame(pixels);
    }

    private static bool ValidId(string id) =>
        id.Length is >= 1 and <= 64
        && id.All(value => char.IsAsciiLetterOrDigit(value) || value is '_' or '-');

    private static DesktopError Invalid(string message) => new("Evaluation.Input", message);
}
