// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioPersonSelectionTests
{
    [AvaloniaTest]
    public async Task TrustedIdentityIsSelectedBeforeCaptureAndCannotBeChangedAfterAcceptance()
    {
        await using var context = new PersonWorkspaceContext();
        context.Enrollment.Plan = CapturePlanChoice.Fingerprints;
        var selection = new StudioPersonSelection(Workspace(context), context.Face);
        var personId = Guid.NewGuid();
        selection.Select(personId, "Selected person", "directory-id").Should().Succeed();
        await context.Fingerprints.AcceptAsync(
            context.Fingerprints.NativeCapture(FingerPosition.RightIndex)
        );
        await context.Fingerprints.AcceptAsync(
            context.Fingerprints.NativeCapture(FingerPosition.LeftIndex)
        );
        var snapshot = context.Export.PrepareAcceptedPackage().Value;
        snapshot.Package.PersonId.Should().Be(personId.ToString("D"));
        snapshot.Package.Label.Should().Be("Selected person");
        selection.Select(personId, "Selected person", "directory-id").Should().Succeed();
        selection
            .Select(Guid.NewGuid(), "Different person", "other")
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Person.CaptureExists");
        context.Person.Name.Should().Be("Selected person");
        context.Person.Identifier = "edited-after-capture";
        context.Person.AuthoritativePersonId.HasValue.Should().BeFalse();
        context
            .Export.ValidateAcceptedPackage(snapshot)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Session.Changed");
    }

    [AvaloniaTest]
    public async Task FrozenReviewCannotBeReassignedAndClearRemovesTheTrustedBinding()
    {
        await using var context = new PersonWorkspaceContext();
        var workspace = Workspace(context);
        var selection = new StudioPersonSelection(workspace, context.Face);
        selection.Select(Guid.NewGuid(), "First person", "").Should().Succeed();
        (await context.Fingerprints.PresentAsync(context.Fingerprints.NativeCapture()))
            .Should()
            .Succeed();
        selection
            .Select(Guid.NewGuid(), "Other person", "")
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Person.CaptureExists");
        await workspace.ClearPersonCommand.ExecuteAsync(default);
        context.Person.AuthoritativePersonId.HasValue.Should().BeFalse();
        selection.Select(Guid.NewGuid(), "Other person", "").Should().Succeed();
        context.Person.Name.Should().Be("Other person");
    }

    private static WorkspaceViewModel Workspace(PersonWorkspaceContext context) =>
        new(
            context.Fingerprints.Capture,
            context.Export,
            context.Fingerprints.Inspection,
            new SettingsViewModel(
                new SettingsStorage("/private/tmp/unused-person-selection-settings.json"),
                _ => Task.FromResult(UnitResult.Success<DesktopError>()),
                NullLogger<SettingsViewModel>.Instance
            )
        );
}
