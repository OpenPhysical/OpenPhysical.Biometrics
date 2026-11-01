// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using System.Collections.Immutable;
using System.Globalization;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ1;
using OpenPhysical.Biometrics.NFIQ2;

// Two impressions of the same finger. Each file contains exactly one grayscale
// byte per pixel, with the original top-left origin.
internal static class FingerprintWorkflowExample
{
    private const string Failure =
        "The workflow failed. Check input geometry and native runtime installation.";

    internal static int Run(string[] args) =>
        Report(
            Dimensions(args)
                .Bind(size => ExampleEdge.TryBind(() => Execute(args, size.Width, size.Height)))
        );

    private static Result<(int Width, int Height), string> Dimensions(string[] args) =>
        int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
        && int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            ? Result.Success<(int Width, int Height), string>((width, height))
            : Result.Failure<(int Width, int Height), string>(Failure);

    private static Result<string, string> Execute(string[] args, int width, int height)
    {
        FingerprintImage[] captures =
        [
            ReadCapture(args[0], width, height),
            ReadCapture(args[1], width, height),
        ];
        IFingerprintEngine engine = new NbisFingerprintEngine();
        var nfiq1 = new Nfiq1Engine();
        using var nfiq2 = new Nfiq2Engine();

        // Materialize each expensive projection once. Reusing the resulting arrays
        // then reuses the assessments and extracted minutiae.
        var firstQuality = captures
            .Select(image =>
                nfiq1.Assess(image.Pixels, image.Width, image.Height, image.DotsPerInch)
            )
            .ToImmutableArray();
        var secondQuality = captures
            .Select(image =>
                nfiq2.Assess(
                    image.Pixels,
                    image.Width,
                    image.Height,
                    image.DotsPerInch,
                    fingerPosition: 0
                )
            )
            .ToImmutableArray();
        var templates = captures.Select(engine.Extract).ToImmutableArray();

        // The assessments retain their distinct algorithm scales and provenance.
        // The application can now apply its acquisition and matching policy.
        _ = (firstQuality, secondQuality);
        return Consistent(engine, captures, templates)
            ? Result.Success<string, string>(
                "Quality, extraction, image/template comparison, and template reuse completed."
            )
            : Result.Failure<string, string>(Failure);
    }

    // Keep probe/gallery order consistent across all four convenience overloads.
    // Image operands are extracted during the call; template operands are reused.
    private static bool Consistent(
        IFingerprintEngine engine,
        FingerprintImage[] captures,
        ImmutableArray<FingerprintTemplate> templates
    )
    {
        int[] scores =
        [
            engine.Match(captures[0], captures[1]),
            engine.Match(captures[0], templates[1]),
            engine.Match(templates[0], captures[1]),
            engine.Match(templates[0], templates[1]),
        ];
        var restored = FingerprintTemplateJson.Deserialize(
            FingerprintTemplateJson.Serialize(templates[0])
        );
        return scores.All(score => score == scores[0])
            && engine.Match(restored, templates[1]) == scores[0];
    }

    private static FingerprintImage ReadCapture(string path, int width, int height)
    {
        var temporary = File.ReadAllBytes(path);
        try
        {
            return new FingerprintImage(temporary, width, height, dotsPerInch: 500);
        }
        finally
        {
            // The immutable capture owns a copy, so clear this temporary file buffer.
            Array.Clear(temporary);
        }
    }

    private static int Report(Result<string, string> result)
    {
        if (result.IsSuccess)
        {
            Console.WriteLine(result.Value);
            return 0;
        }
        Console.Error.WriteLine(result.Error);
        return 1;
    }
}
