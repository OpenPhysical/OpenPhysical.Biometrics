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
        foreach (var item in Cases(engine, id => images[id].Load()))
        {
            var accepted = Decide(engine, created.Value, reference, referenceTemplate, item);
            if (accepted.IsFailure)
            {
                Console.Error.WriteLine(accepted.Error);
                return 1;
            }
            Console.WriteLine($"Policy {created.Value.Identifier}: accepted={accepted.Value}");
        }
        return 0;
    }

    // This policy belongs to the supplied regression assortment. The paired
    // impressions and known nonmates demonstrate an application's decision
    // boundary while retaining the selected policy identity in every result.
    // Extraction is materialized once because the templates are reused below.
    private static ImmutableArray<(
        FingerprintImage Capture,
        FingerprintTemplate Template,
        bool Expected
    )> Cases(IFingerprintEngine engine, Func<string, FingerprintImage> load) =>
        new[]
        {
            (Id: "s0005_03", Expected: true),
            (Id: "f0070_10", Expected: false),
            (Id: "f0003_10", Expected: false),
        }
            .Select(item => (Capture: load(item.Id), item.Expected))
            .Select(item => (item.Capture, Template: engine.Extract(item.Capture), item.Expected))
            .ToImmutableArray();

    // Image-based overloads perform their own extraction for the requested comparison.
    private static CSharpFunctionalExtensions.Result<bool> Decide(
        IFingerprintEngine engine,
        FingerprintMatchingPolicy policy,
        FingerprintImage reference,
        FingerprintTemplate referenceTemplate,
        (FingerprintImage Capture, FingerprintTemplate Template, bool Expected) item
    )
    {
        int[] scores =
        [
            engine.Match(item.Capture, reference),
            engine.Match(item.Capture, referenceTemplate),
            engine.Match(item.Template, reference),
            engine.Match(item.Template, referenceTemplate),
        ];
        var results = scores.Select(policy.Evaluate).ToImmutableArray();
        return results.Any(result => result.IsFailure || result.Value.Accepted != item.Expected)
            ? CSharpFunctionalExtensions.Result.Failure<bool>(
                "The fixture comparison decision changed."
            )
            : results[0].Value.Accepted;
    }
}
