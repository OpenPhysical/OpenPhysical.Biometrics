// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Globalization;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ1;
using OpenPhysical.Biometrics.NFIQ2;

if (args is ["--sd04", var fixtureDirectory])
    return OpenPhysical.Biometrics.Examples.Sd04MatchingExample.Run(fixtureDirectory);

if (args.Length != 4)
{
    Console.WriteLine("Examples <first.gray8> <second.gray8> <width> <height>");
    return 2;
}

try
{
    // These inputs are two impressions of the same finger. Each file contains
    // exactly one grayscale byte per pixel, with the original top-left origin.
    var width = int.Parse(args[2], CultureInfo.InvariantCulture);
    var height = int.Parse(args[3], CultureInfo.InvariantCulture);
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
        .Select(image => nfiq1.Assess(image.Pixels, image.Width, image.Height, image.DotsPerInch))
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

    // Keep probe/gallery order consistent across all four convenience overloads.
    // Image operands are extracted during the call; template operands are reused.
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
    if (
        scores.Any(score => score != scores[0])
        || engine.Match(restored, templates[1]) != scores[0]
    )
        throw new InvalidOperationException("Comparison workflows differed.");

    // The assessments retain their distinct algorithm scales and provenance.
    // The application can now apply its acquisition and matching policy.
    _ = firstQuality;
    _ = secondQuality;
    Console.WriteLine(
        "Quality, extraction, image/template comparison, and template reuse completed."
    );
    return 0;
}
catch (Exception error)
    when (error
            is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or NotSupportedException
                or FormatException
                or OverflowException
                or DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
    )
{
    Console.Error.WriteLine(
        "The workflow failed. Check input geometry and native runtime installation."
    );
    return 1;
}

static FingerprintImage ReadCapture(string path, int width, int height)
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
