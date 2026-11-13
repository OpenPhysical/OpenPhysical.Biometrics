// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Binds trusted person selection to the existing capture workspace before acquisition.</summary>
public sealed class StudioPersonSelection
{
    private readonly WorkspaceViewModel workspace;
    private readonly FaceCaptureViewModel face;

    /// <summary>Connects authoritative selection to the shared person and existing sources.</summary>
    /// <param name="workspace">The shared capture and accepted-person state.</param>
    /// <param name="face">The existing camera workspace.</param>
    public StudioPersonSelection(WorkspaceViewModel workspace, FaceCaptureViewModel face)
    {
        this.workspace = workspace;
        this.face = face;
    }

    /// <summary>Selects an authenticated application identity before capturing this person.</summary>
    /// <param name="personId">Directory or server person identifier.</param>
    /// <param name="name">Authoritative display name.</param>
    /// <param name="identifier">Authoritative external identifier, or an empty string.</param>
    /// <returns>Success or an explicit request to clear the previously captured person.</returns>
    public UnitResult<DesktopError> Select(Guid personId, string name, string identifier)
    {
        var enrollment = workspace.Export.Enrollment;
        var person = enrollment.Person;
        if (
            person.AuthoritativePersonId.HasValue
            && person.AuthoritativePersonId.Value == personId
            && person.Name == name
            && person.Identifier == identifier
        )
            return UnitResult.Success<DesktopError>();
        if (
            workspace.Capture.HasActiveAttempt
            || face.HasCapture
            || enrollment.IsBusy
            || enrollment.IsEditing
            || enrollment.Sections.Any(section => section.AcceptedCount != 0)
            || workspace.Export.SaveCommand.IsRunning
            || workspace.Export.OpenCommand.IsRunning
        )
            return new DesktopError(
                "Person.CaptureExists",
                "Clear person before selecting a different authoritative identity."
            );
        return person.Bind(personId, name, identifier);
    }
}
