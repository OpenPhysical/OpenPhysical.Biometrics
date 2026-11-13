// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;

internal sealed class FaceCaptureNotifications
{
    private ImmutableArray<CameraSource> sources;
    private Maybe<CameraSource> source;
    private Maybe<CameraMode> mode;

    internal UnitResult<DesktopError> Refresh(
        FaceCaptureWorkflow workflow,
        FaceImageViewModel guides,
        ImmutableArray<IAsyncRelayCommand> commands,
        Action<string> notify
    )
    {
        guides.Update(workflow);
        foreach (var name in Changes(workflow.Selection))
            notify(name);
        foreach (var name in FacePresentation.Properties)
            notify(name);
        foreach (var command in commands)
            command.NotifyCanExecuteChanged();
        return UnitResult.Success<DesktopError>();
    }

    private ImmutableArray<string> Changes(FaceCameraSelection selection)
    {
        var changed = ImmutableArray.CreateBuilder<string>();
        if (!sources.Equals(selection.Sources))
            changed.Add(nameof(FaceCaptureViewModel.Sources));
        var sourceChanged = !source.Match(
            value => ReferenceEquals(value, selection.Source),
            () => false
        );
        if (sourceChanged)
        {
            changed.Add(nameof(FaceCaptureViewModel.SelectedSource));
            changed.Add(nameof(FaceCaptureViewModel.Modes));
        }
        if (
            sourceChanged
            || !mode.Match(value => ReferenceEquals(value, selection.Mode), () => false)
        )
            changed.Add(nameof(FaceCaptureViewModel.SelectedMode));
        sources = selection.Sources;
        source = selection.Source;
        mode = selection.Mode;
        return changed.ToImmutable();
    }
}
