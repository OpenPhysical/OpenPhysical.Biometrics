// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class ComparisonInvalidationTests
{
    [AvaloniaTest]
    public async Task ClearingAReassignedProbeRemovesOldDetailsButKeepsTheCredentialReference()
    {
        var dependencies = new ControlledInspectionDependencies();
        dependencies.Release();
        var view = NewView(dependencies);
        await LoadCredentialAsync(view);
        var reference = view.Scene.Value;
        var original = dependencies.Captured.Value;
        await view.CompareCommand.ExecuteAsync(default);
        view.Report.Should().Contain("Reference template");
        view.Summary.Should().Be("Match");
        view.Decision.IsMatch.Should().BeTrue();
        view.AssignedProbeFinger = FingerChoice.For(FingerPosition.RightThumb);
        view.Decision.HasDecision.Should().BeFalse();
        view.ProbeSummary.Should().Contain("Comparison finger: Right Thumb");
        view.ProbeSummary.Should().Contain("Original capture label: Right Index");
        const string oldQuality = "NFIQ";
        view.ProbeSummary.Should().Contain(oldQuality);
        dependencies.SetCapture(Maybe<CapturedFingerprint>.None);
        view.ProbeSummary.Should().Contain("Comparison finger: Right Thumb");
        view.ProbeSummary.Should().Contain("No frozen capture");
        view.ProbeSummary.Should().NotContain("Original capture").And.NotContain(oldQuality);
        view.Report.Should().NotContain("Bozorth3 score");
        view.Summary.Should().NotBe("Match");
        view.Decision.HasDecision.Should().BeFalse();
        view.ProbeScene.HasNoValue.Should().BeTrue();
        view.Scene.Value.Should().BeSameAs(reference);
        view.Reference.SelectedView.Finger.Should().Be(FingerPosition.RightIndex);
        original.Finger.Should().Be(FingerPosition.RightIndex);
        await view.StopAsync();
    }

    [AvaloniaTest]
    [TestCase("assignment")]
    [TestCase("capture")]
    [TestCase("threshold")]
    public async Task AChangedThenRestoredProbeStateCannotResurrectAPendingDecision(string change)
    {
        var dependencies = new ControlledInspectionDependencies();
        var view = NewView(dependencies);
        await LoadCredentialAsync(view);
        var reference = view.Scene.Value;
        var comparing = view.CompareCommand.ExecuteAsync(default);
        await dependencies.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ChangeAndRestore(view, dependencies, change);
        var cleared = view.Report;
        cleared.Should().NotContain("Bozorth3 score");
        dependencies.Release();
        await comparing;
        view.Report.Should().Be(cleared);
        view.Summary.Should().NotBe("Match");
        view.Decision.HasDecision.Should().BeFalse();
        view.Scene.Value.Should().BeSameAs(reference);
        view.ProbeScene.Value.ReferenceOverlay.Should().BeEmpty();
        dependencies.Captured.Value.Finger.Should().Be(FingerPosition.RightIndex);
        await view.StopAsync();
    }

    [AvaloniaTest]
    [TestCase(40, true)]
    [TestCase(43, false)]
    public async Task CurrentDecisionStylesComeFromTheActualPolicyOutcome(
        int threshold,
        bool accepted
    )
    {
        var dependencies = new ControlledInspectionDependencies();
        dependencies.Release();
        var view = NewView(dependencies);
        await LoadCredentialAsync(view);
        view.Threshold = threshold;
        await view.CompareCommand.ExecuteAsync(default);
        view.Decision.HasDecision.Should().BeTrue();
        view.Decision.Accepted.Value.Should().Be(accepted);
        view.Decision.IsMatch.Should().Be(accepted);
        view.Decision.IsNoMatch.Should().Be(!accepted);
        view.Decision.Label.Should().Be(accepted ? "Match" : "No match");
        await view.StopAsync();
    }

    [AvaloniaTest]
    [TestCase("capture")]
    [TestCase("threshold")]
    [TestCase("reference")]
    [TestCase("replacement")]
    public async Task CurrentDecisionIsRemovedWhenItsInputsAreInvalidated(string change)
    {
        var dependencies = new ControlledInspectionDependencies();
        dependencies.Release();
        var view = NewView(dependencies);
        await LoadCredentialAsync(view);
        await view.CompareCommand.ExecuteAsync(default);
        view.Decision.HasDecision.Should().BeTrue();
        if (change == "capture")
            dependencies.SetCapture(Maybe<CapturedFingerprint>.None);
        else if (change == "threshold")
            view.Threshold = 41;
        else if (change == "reference")
            view.Credential.SelectedReader = "Other sample reader";
        else
            await view.LoadCommand.ExecuteAsync(default);
        view.Decision.HasDecision.Should().BeFalse();
        view.Decision.IsMatch.Should().BeFalse();
        view.Decision.IsNoMatch.Should().BeFalse();
        view.Decision.Label.Should().BeEmpty();
        await view.StopAsync();
    }

    private static InspectionViewModel NewView(ControlledInspectionDependencies dependencies) =>
        new(
            dependencies,
            new DesktopFilePicker(),
            dependencies,
            new TemplateComparison(new FingerprintComparisonService(dependencies)),
            new CredentialReaderService(new DocumentCredentialTransport(dependencies.Document))
        );

    private static async Task LoadCredentialAsync(InspectionViewModel view)
    {
        await view.Credential.RefreshCommand.ExecuteAsync(default);
        view.Credential.Pin = "123456";
        await view.Credential.ReadCommand.ExecuteAsync(default);
    }

    private static UnitResult<DesktopError> ChangeAndRestore(
        InspectionViewModel view,
        ControlledInspectionDependencies dependencies,
        string change
    )
    {
        if (change == "assignment")
        {
            view.AssignedProbeFinger = FingerChoice.For(FingerPosition.RightThumb);
            view.AssignedProbeFinger = FingerChoice.For(FingerPosition.RightIndex);
        }
        else if (change == "capture")
        {
            var original = dependencies.Captured;
            dependencies.SetCapture(Maybe<CapturedFingerprint>.None);
            dependencies.SetCapture(original);
        }
        else
        {
            view.Threshold = 41;
            view.Threshold = 40;
        }
        return UnitResult.Success<DesktopError>();
    }
}
