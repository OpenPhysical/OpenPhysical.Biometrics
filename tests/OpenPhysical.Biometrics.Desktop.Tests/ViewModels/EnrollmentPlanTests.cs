// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class EnrollmentPlanTests
{
    [AvaloniaTest]
    public async Task SuggestionsAreStableEditableAndRequestedOnlyForANewPerson()
    {
        var random = new SequenceRandom([0, 1, 7, 1]);
        await using var context = new EnrollmentOperatorContext(
            new ControlledExportStorage(),
            new EnrollmentFingerSuggestions(random)
        );
        var enrollment = context.Export.Enrollment;
        var model = enrollment.Fingerprints;
        model.PrimaryFinger.Position.Should().Be(FingerPosition.RightThumb);
        model.SecondaryFinger.Position.Should().Be(FingerPosition.LeftIndex);
        enrollment.Person.Name = "Sample person";
        enrollment.Person.Identifier = "sample-17";
        model.PrimaryFinger = FingerChoice.For(FingerPosition.RightRing);
        model.TwoFingers = false;
        _ = model.Instructions;
        _ = model.PrimaryLabel;
        random.Count.Should().Be(2);
        await context.Export.ClearPersonCommand.ExecuteAsync(default);
        random.Count.Should().Be(4);
        enrollment.Person.Name.Should().BeEmpty();
        enrollment.Person.Identifier.Should().BeEmpty();
        model.TwoFingers.Should().BeTrue();
        model.PrimaryFinger.Position.Should().Be(FingerPosition.LeftMiddle);
        model.SecondaryFinger.Position.Should().Be(FingerPosition.RightIndex);
        enrollment.Ready.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task SuccessfulCaptureWaitsForExplicitAcceptanceAndExportsTheOriginalSnapshots()
    {
        var storage = new ControlledExportStorage();
        storage.Completed.SetResult(Result.Success<string, DesktopError>("saved.fmr"));
        await using var context = new EnrollmentOperatorContext(
            storage,
            EnrollmentTestSuggestions.IndexPair()
        );
        var model = context.Export.Enrollment.Fingerprints;
        context.Export.Enrollment.Person.Name = "Sample person";
        var first = StorageFixtures.Capture(FingerPosition.RightIndex);
        await context.PresentAsync(first);
        model.PrimaryLabel.Should().Be("Waiting for acceptance");
        model.SecondaryLabel.Should().Be("Waiting for capture");
        model.Progress.Should().Be("0 of 2 fingerprints accepted");
        await model.AcceptCommand.ExecuteAsync(default);
        model.PrimaryLabel.Should().Be("Right Index accepted");
        var second = StorageFixtures.Capture(FingerPosition.LeftIndex);
        await context.PresentAsync(second);
        model.SecondaryLabel.Should().Be("Waiting for acceptance");
        model.PrimaryLabel.Should().Be("Right Index accepted");
        await model.AcceptCommand.ExecuteAsync(default);
        await context.Export.SaveCommand.ExecuteAsync(default);
        storage.Accepted.Should().Equal(first, second);
    }

    [Test]
    public void EverySuggestedPairUsesOppositeHandsWithoutRestrictingManualChoices()
    {
        var pairs = new HashSet<(FingerPosition First, FingerPosition Second)>();
        for (var first = 0; first < 10; first++)
        for (var second = 0; second < 5; second++)
        {
            var plan = new EnrollmentFingerSuggestions(new PairRandom(first, second)).Next().Plan;
            plan.Distinct().Should().HaveCount(2);
            ((byte)plan[0] <= 5).Should().NotBe((byte)plan[1] <= 5);
            pairs.Add((plan[0], plan[1]));
        }
        pairs.Should().HaveCount(50);
        EnrollmentSession
            .Create([FingerPosition.RightIndex, FingerPosition.RightMiddle])
            .Should()
            .Succeed();
    }

    private sealed class SequenceRandom(IEnumerable<int> values) : Random
    {
        private readonly Queue<int> values = new(values);
        internal int Count { get; private set; }

        public override int Next(int maximum)
        {
            Count++;
            return values.Dequeue();
        }
    }

    private sealed class PairRandom(int first, int second) : Random
    {
        public override int Next(int maximum) => maximum == 10 ? first : second;
    }
}
