// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;

internal sealed class FaceCameraSetupOperations(
    FaceCaptureWorkflow workflow,
    Func<UnitResult<DesktopError>> changed
)
{
    internal bool Busy { get; private set; }

    internal Task RefreshAsync() => ChangeAsync(true);

    internal Task RestartAsync() => ChangeAsync(false);

    internal UnitResult<DesktopError> Select(CameraSource source) =>
        CanSelect() && source is not null && source != workflow.Selection.Source
            ? RestartSelected(workflow.Selection.Select(source))
            : UnitResult.Success<DesktopError>();

    internal UnitResult<DesktopError> Select(CameraMode mode) =>
        CanSelect() && mode is not null && mode != workflow.Selection.Mode
            ? RestartSelected(workflow.Selection.Select(mode))
            : UnitResult.Success<DesktopError>();

    private bool CanSelect() => !Busy && !workflow.Busy && workflow.Draft.Source.HasNoValue;

    private UnitResult<DesktopError> RestartSelected(UnitResult<DesktopError> selected)
    {
        if (selected.IsSuccess)
            _ = RestartAsync();
        return selected;
    }

    private async Task ChangeAsync(bool discover)
    {
        Busy = true;
        changed();
        try
        {
            var attempt = await DesktopEdge.TryAsync(
                () => ChangeSourceAsync(discover),
                "Face.Source",
                "The camera could not be changed. Reconnect it and try again."
            );
            if (attempt.IsFailure)
                workflow.Fail(attempt.Error);
            else if (attempt.Value.IsFailure)
                workflow.Fail(attempt.Value.Error);
        }
        finally
        {
            Busy = false;
            changed();
        }
    }

    private async Task<UnitResult<DesktopError>> ChangeSourceAsync(bool discover)
    {
        var paused = await workflow.ActivateAsync(false);
        if (paused.IsFailure)
            return paused;
        var refreshed = discover
            ? await workflow.Selection.RefreshAsync()
            : UnitResult.Success<DesktopError>();
        return refreshed.IsFailure ? refreshed : await workflow.ActivateAsync(true);
    }
}
