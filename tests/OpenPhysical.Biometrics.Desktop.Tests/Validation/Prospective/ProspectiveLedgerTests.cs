// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.NBIS;

[TestFixture]
internal sealed class ProspectiveLedgerTests
{
    [Test]
    public void MissingAttemptKeepsEveryPairAndSerializesNullScores()
    {
        ImmutableArray<ProspectiveImage> images =
        [
            Image("a", "p1", "f1"),
            Image("b", "p1", "f1"),
            Image("c", "p2", "f2", true),
        ];
        var pairs = ProspectivePairs
            .Create(
                images,
                System.Collections.Immutable.ImmutableArray.CreateRange(
                    Enum.GetValues<ProspectiveArm>()
                ),
                (_, _, _) => Result.Success<int, DesktopError>(45)
            )
            .Value;
        var ledger = ProspectiveLedger.Create(
            images,
            pairs,
            ProspectiveArm.Candidate,
            ImmutableDictionary<string, string>.Empty
        );
        pairs.Should().HaveCount(3);
        ledger.Processed.GenuineAccepted.Should().Be(1);
        ledger.Processed.UnavailableBetweenPersonPairs.Should().Be(2);
        ledger.UnavailableProcessedImages.Should().Be(1);
        ledger
            .Pairs.Where(pair => pair.RightId == "c")
            .Should()
            .OnlyContain(pair => pair.ProcessedScore.HasNoValue && !pair.ProcessedAccepted);
        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(ledger, ProspectiveJson.Options)
        );
        json.RootElement.GetProperty("Pairs")[1]
            .GetProperty("ProcessedScore")
            .ValueKind.Should()
            .Be(JsonValueKind.Null);
    }

    [Test]
    public void EveryUnorderedPairHasOneCorrectPersonCategoryAndGainsLosses()
    {
        ImmutableArray<ProspectiveImage> images =
        [
            Image("a", "p1", "f1"),
            Image("b", "p1", "f1"),
            Image("c", "p1", "f2"),
            Image("d", "p2", "f3"),
        ];
        var pairs = ProspectivePairs
            .Create(
                images,
                System.Collections.Immutable.ImmutableArray.CreateRange(
                    Enum.GetValues<ProspectiveArm>()
                ),
                (_, _, arm) =>
                    Result.Success<int, DesktopError>(arm == ProspectiveArm.RawWhole ? 39 : 40)
            )
            .Value;
        var ledger = ProspectiveLedger.Create(
            images,
            pairs,
            ProspectiveArm.Candidate,
            ImmutableDictionary<string, string>.Empty
        );
        ledger.Pairs.Should().HaveCount(6);
        ledger.GenuinePairs.Should().Be(1);
        ledger.WithinPersonDifferentFingerPairs.Should().Be(2);
        ledger.BetweenPersonPairs.Should().Be(3);
        ledger.GenuineGained.Should().Be(1);
        ledger.GenuineLost.Should().Be(0);
        ledger.Processed.ImpostorAccepted.Should().Be(5);
        ledger.Passed.Should().BeFalse();
    }

    [Test]
    public void OperationalMatcherFailureAbortsInsteadOfCreatingUnavailablePairs()
    {
        ImmutableArray<ProspectiveImage> images = [Image("a", "p1", "f1"), Image("b", "p1", "f1")];
        var result = ProspectivePairs.Create(
            images,
            System.Collections.Immutable.ImmutableArray.CreateRange(
                Enum.GetValues<ProspectiveArm>()
            ),
            (_, _, _) => Result.Failure<int, DesktopError>(new("EngineFailed", "native failure"))
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EngineFailed");
    }

    private static ProspectiveImage Image(
        string id,
        string person,
        string finger,
        bool rejected = false
    )
    {
        var template = new FingerprintTemplate(800, 750, [], "test ledger only");
        return new(
            id,
            person,
            finger,
            Enum.GetValues<ProspectiveArm>()
                .Select(arm => new ProspectiveRepresentation(
                    arm,
                    rejected && arm != ProspectiveArm.RawWhole
                        ? Maybe<FingerprintTemplate>.None
                        : template,
                    rejected && arm != ProspectiveArm.RawWhole
                        ? new DesktopError("Capture.UnclearContact", "unclear")
                        : Maybe<DesktopError>.None
                ))
                .ToImmutableArray()
        );
    }
}
