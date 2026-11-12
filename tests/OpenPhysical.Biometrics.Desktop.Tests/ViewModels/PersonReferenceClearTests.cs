// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class PersonReferenceClearTests
{
    [AvaloniaTest]
    public async Task ClearPersonClearsBothScenesReferenceChoiceAndScoreAndUsesTheFreshPlanLabel()
    {
        var dependencies = new ControlledInspectionDependencies();
        dependencies.Release();
        var view = NewView(dependencies);
        view.FilePath = "sample.fmr";
        await view.LoadCommand.ExecuteAsync(default);
        await view.CompareCommand.ExecuteAsync(default);
        view.Report.Should().Contain("Bozorth3 score");
        view.Credential.Pin = "123456";
        var next = FingerChoice.For(FingerPosition.LeftMiddle);
        (await view.ClearPersonAsync(next)).Should().Succeed();
        view.Scene.HasNoValue.Should().BeTrue();
        view.ProbeScene.HasNoValue.Should().BeTrue();
        view.Reference.Views.Should().BeEmpty();
        view.Reference.SelectedView.Index.Should().Be(-1);
        view.AssignedProbeFinger.Should().Be(next);
        view.FilePath.Should().BeEmpty();
        view.Credential.Pin.Should().BeEmpty();
        view.Credential.Status.Should().NotContain("loaded");
        view.Report.Should().NotContain("Bozorth3 score");
        await view.StopAsync();
    }

    [AvaloniaTest]
    public async Task ClearPersonInvalidatesAndDrainsAnOldComparisonBeforeItCanPublish()
    {
        var dependencies = new ControlledInspectionDependencies();
        var view = NewView(dependencies);
        await view.LoadCommand.ExecuteAsync(default);
        var comparing = view.CompareCommand.ExecuteAsync(default);
        await dependencies.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var clearing = view.ClearPersonAsync(FingerChoice.For(FingerPosition.LeftRing));
        view.Scene.HasNoValue.Should().BeTrue();
        clearing.IsCompleted.Should().BeFalse();
        dependencies.Release();
        (await clearing).Should().Succeed();
        await comparing;
        view.Report.Should().NotContain("Bozorth3 score");
        view.Scene.HasNoValue.Should().BeTrue();
        await view.StopAsync();
    }

    private static InspectionViewModel NewView(ControlledInspectionDependencies dependencies) =>
        new(
            dependencies,
            new DesktopFilePicker(),
            dependencies,
            new TemplateComparison(new FingerprintComparisonService(dependencies)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );
}
