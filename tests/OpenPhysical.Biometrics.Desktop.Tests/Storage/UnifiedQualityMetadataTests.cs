// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class UnifiedQualityMetadataTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task OptionalNfiq2RoundTripsWithoutChangingTheFmrOrExistingQuality(bool include)
    {
        var original = ProcessingStorageFixtures.Capture();
        var capture = WithAssessment(original, include);
        var record = UnsignedTemplateService
            .Create([original], new TemplateMetadata(1, 0, 0))
            .Value;
        var folder = NewFolder();
        var store = new TemplateStorage();
        var saved = await store.SaveAsync(record, [capture], folder, CancellationToken.None);
        saved.Should().Succeed();
        (await File.ReadAllBytesAsync(saved.Value))
            .Should()
            .Equal(Incits378Codec.Write(record).Value);
        using var manifest = JsonDocument.Parse(
            await File.ReadAllBytesAsync(Path.ChangeExtension(saved.Value, ".json"))
        );
        var image = manifest.RootElement.GetProperty("Images")[0];
        image.TryGetProperty("Nfiq2", out _).Should().Be(include);
        image
            .GetProperty("Quality")
            .GetProperty("Nfiq1")
            .GetInt32()
            .Should()
            .Be(original.Quality.Nfiq1);
        var loaded = await store.LoadAsync(saved.Value, CancellationToken.None);
        loaded.Should().Succeed();
        AssertReopened(loaded.Value, original, record, include);
        Directory.Delete(folder, true);
    }

    private static CapturedFingerprint WithAssessment(CapturedFingerprint original, bool include)
    {
        var assessment = include
            ? CSharpFunctionalExtensions.Maybe.From(
                FingerprintUnifiedQualityAssessment.Create(73, "test-model-1").Value
            )
            : CSharpFunctionalExtensions.Maybe<FingerprintUnifiedQualityAssessment>.None;
        var evidence = original.Processing.Value;
        var selected = evidence.Candidates.Single();
        return original with
        {
            Nfiq2 = assessment,
            Processing = evidence with
            {
                Candidates =
                [
                    selected with
                    {
                        Quality = selected.Quality with { Nfiq2 = assessment },
                    },
                ],
            },
        };
    }

    private static FingerprintRecord AssertReopened(
        TemplateDocument document,
        CapturedFingerprint original,
        FingerprintRecord record,
        bool include
    )
    {
        var assessment = document.Images[0].Nfiq2;
        assessment.HasValue.Should().Be(include);
        if (include)
        {
            assessment.Value.Score.Should().Be(73);
            assessment.Value.AlgorithmVersion.Should().Be("test-model-1");
        }
        document.Images[0].GrayPixels.Should().Equal(original.Processing.Value.Artifact.Pixels);
        Incits378Codec
            .Write(document.Record)
            .Value.Should()
            .Equal(Incits378Codec.Write(record).Value);
        return document.Record;
    }

    [TestCase("null")]
    [TestCase("{\"Score\":101,\"AlgorithmVersion\":\"test-model-1\"}")]
    [TestCase("{\"Score\":-1,\"AlgorithmVersion\":\"test-model-1\"}")]
    [TestCase("{\"Score\":73.5,\"AlgorithmVersion\":\"test-model-1\"}")]
    [TestCase("{\"AlgorithmVersion\":\"test-model-1\"}")]
    [TestCase("{\"Score\":73,\"AlgorithmVersion\":\"\"}")]
    [TestCase("{\"Score\":73,\"AlgorithmVersion\":null}")]
    public async Task MalformedPresentSupplementalQualityFailsInsteadOfBecomingAbsent(
        string quality
    )
    {
        var capture = ProcessingStorageFixtures.Capture();
        var record = UnsignedTemplateService.Create([capture], new TemplateMetadata(1, 0, 0)).Value;
        var folder = NewFolder();
        var store = new TemplateStorage();
        var saved = (
            await store.SaveAsync(record, [capture], folder, CancellationToken.None)
        ).Value;
        var path = Path.ChangeExtension(saved, ".json");
        var manifest = JsonNode.Parse(await File.ReadAllBytesAsync(path))!;
        manifest["Images"]![0]!["Nfiq2"] = JsonNode.Parse(quality);
        await File.WriteAllTextAsync(path, manifest.ToJsonString());
        (await store.LoadAsync(saved, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    private static string NewFolder() =>
        Path.Combine(
            PrivateTestPaths.Root,
            "biometrics-unified-quality-" + Guid.NewGuid().ToString("N")
        );
}
