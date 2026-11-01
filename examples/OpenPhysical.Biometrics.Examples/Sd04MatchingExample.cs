// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NBIS.Fixtures;

internal static class Sd04MatchingExample
{
    internal static int Run(string fixtureDirectory)
    {
        var fixtures = new Sd04Fixtures(fixtureDirectory);
        var images = fixtures
            .Pairs.SelectMany(pair => new[] { pair.First, pair.Second })
            .ToDictionary(image => image.Id);
        IFingerprintEngine engine = new NbisFingerprintEngine();
        var reference = images["f0005_03"].Load();
        var referenceTemplate = engine.Extract(reference);
        var created = FingerprintMatchingPolicy.Create("sd04-assortment-regression-v1", 40);
        if (created.IsFailure)
        {
            Console.Error.WriteLine(created.Error.Message);
            return 1;
        }
        var policy = created.Value;

        // This policy belongs to the supplied regression assortment. The paired
        // impressions and known nonmates demonstrate an application's decision
        // boundary while retaining the selected policy identity in every result.
        var cases = new[]
        {
            (Image: images["s0005_03"], Expected: true),
            (Image: images["f0070_10"], Expected: false),
            (Image: images["f0003_10"], Expected: false),
        }
            .Select(item => (Capture: item.Image.Load(), item.Expected))
            .ToImmutableArray();

        // Materialize extraction once when templates will be reused. Image-based
        // overloads perform their own extraction for the requested comparison.
        var reusable = cases
            .Select(item => (item.Capture, Template: engine.Extract(item.Capture), item.Expected))
            .ToImmutableArray();
        foreach (var item in reusable)
        {
            int[] scores =
            [
                engine.Match(item.Capture, reference),
                engine.Match(item.Capture, referenceTemplate),
                engine.Match(item.Template, reference),
                engine.Match(item.Template, referenceTemplate),
            ];
            var results = scores.Select(policy.Evaluate).ToImmutableArray();
            if (results.Any(result => result.IsFailure || result.Value.Accepted != item.Expected))
            {
                Console.Error.WriteLine("The fixture comparison decision changed.");
                return 1;
            }
            Console.WriteLine($"Policy {policy.Identifier}: accepted={results[0].Value.Accepted}");
        }
        return 0;
    }
}
