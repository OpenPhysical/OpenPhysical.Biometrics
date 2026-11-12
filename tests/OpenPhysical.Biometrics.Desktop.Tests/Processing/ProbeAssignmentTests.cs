// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class ProbeAssignmentTests
{
    [Test]
    public void AssignmentIsPartOfTheProjectionCacheKeyAndPreservesAllPoints()
    {
        var engine = new ControlledInspectionDependencies();
        engine.Release();
        var capture = engine.Captured.Value;
        var service = new TemplateComparison(new FingerprintComparisonService(engine));
        service
            .Compare(capture, FingerPosition.RightIndex, engine.Document.Record, 0, 40)
            .Should()
            .Succeed();
        var first = engine.ComparedProbe.Value;
        var left = ControlledInspectionDependencies.DocumentFor(FingerPosition.LeftIndex);
        service.Compare(capture, FingerPosition.LeftIndex, left.Record, 0, 40).Should().Succeed();
        engine.ComparedProbe.Value.Should().NotBeSameAs(first);
        engine.ComparedProbe.Value.Minutiae.Should().Equal(first.Minutiae);
        capture.Finger.Should().Be(FingerPosition.RightIndex);
    }

    [Test]
    public void ComparisonAssignmentRelabelsOnlyTheCanonicalProjection()
    {
        var engine = new ControlledInspectionDependencies();
        engine.Release();
        var capture = engine.Captured.Value;
        var originalView = capture.View;
        var originalPixels = capture.Frame.Pixels;
        var service = new TemplateComparison(new FingerprintComparisonService(engine));
        var left = ControlledInspectionDependencies.DocumentFor(FingerPosition.LeftIndex);
        service.Compare(capture, FingerPosition.LeftIndex, left.Record, 0, 40).Should().Succeed();
        engine.Entered.Task.IsCompleted.Should().BeTrue();
        capture.Finger.Should().Be(FingerPosition.RightIndex);
        capture.View.Should().BeSameAs(originalView);
        capture.View.FingerPosition.Should().Be(2);
        capture.Frame.Pixels.Should().Equal(originalPixels);
    }
}
