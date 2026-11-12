// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.NBIS;

[TestFixture]
internal sealed class ProspectiveSerializationTests
{
    [Test]
    public void AvailableAndUnavailableAttemptsRoundTripWithoutReadingAbsentOptionalValues()
    {
        var report = Report();
        var json = JsonSerializer.Serialize(report, ProspectiveJson.Options);
        using var parsed = JsonDocument.Parse(json);
        using var roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(parsed.RootElement));
        Check(roundTrip.RootElement);
        json.Should().NotContain("\"HasValue\"").And.NotContain("\"Template\"");
    }

    private static ProspectiveRepresentation AvailableImage()
    {
        return new ProspectiveRepresentation(
            ProspectiveArm.RawWhole,
            new FingerprintTemplate(800, 750, [], "serialization fixture only"),
            Maybe<DesktopError>.None
        )
        {
            Evidence = new ProspectiveImageEvidence(
                "source",
                "assessed",
                "hull",
                0,
                25,
                Maybe<int>.None,
                Maybe<int>.None,
                []
            ),
        };
    }

    private static ProspectiveReport Report()
    {
        var rejected = new ProspectiveRepresentation(
            ProspectiveArm.RawWhole,
            Maybe<FingerprintTemplate>.None,
            new DesktopError("Capture.ContactTooLight", "Too light")
        );
        return new ProspectiveReport(
            "serialization-development",
            "frozen",
            "manifest",
            [new("a", "p1", "f1", [AvailableImage()]), new("b", "p2", "f2", [rejected])],
            [
                new(
                    "a",
                    "b",
                    false,
                    true,
                    [
                        new(ProspectiveArm.RawWhole, Maybe<int>.None),
                        new(ProspectiveArm.Candidate, 42),
                    ]
                ),
            ],
            [],
            0,
            0,
            0
        );
    }

    private static bool Check(JsonElement report)
    {
        var available = report.GetProperty("Images")[0].GetProperty("Representations")[0];
        available.GetProperty("Available").GetBoolean().Should().BeTrue();
        available.GetProperty("Diagnostics").GetArrayLength().Should().Be(0);
        available
            .GetProperty("Assessment")[0]
            .GetProperty("ExtractedFeatures")
            .ValueKind.Should()
            .Be(JsonValueKind.Null);
        var rejected = report.GetProperty("Images")[1].GetProperty("Representations")[0];
        rejected.GetProperty("Available").GetBoolean().Should().BeFalse();
        rejected
            .GetProperty("Diagnostics")[0]
            .GetProperty("Code")
            .GetString()
            .Should()
            .Be("Capture.ContactTooLight");
        var scores = report.GetProperty("Pairs")[0].GetProperty("Scores");
        scores[0].GetProperty("Score").ValueKind.Should().Be(JsonValueKind.Null);
        scores[1].GetProperty("Score").GetInt32().Should().Be(42);
        var restored = JsonSerializer.Deserialize<ProspectiveScore[]>(
            scores.GetRawText(),
            ProspectiveJson.Options
        )!;
        restored[0].Score.HasNoValue.Should().BeTrue();
        restored[1].Score.Value.Should().Be(42);
        return true;
    }
}
