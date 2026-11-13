// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

internal sealed class FaceEnrollmentSection(FaceCaptureViewModel face)
    : EnrollmentSectionState(face),
        IEnrollmentSection
{
    public string Id => "face";
    public string Title => "Face";
    public bool Complete => face.Complete;
    public bool IsBusy => face.IsBusy;
    public bool IsEditing => false;
    public bool CanReset => face.CanReset;
    public string Progress => face.Progress;
    public string Instructions => face.Instructions;
    public ObservableObject ViewModel => face;
    public IAsyncRelayCommand AcceptCommand => face.AcceptCommand;
    public ImmutableArray<IAsyncRelayCommand> Commands => face.Commands;

    public Task<UnitResult<DesktopError>> ActivateAsync(bool active) => face.ActivateAsync(active);

    public Task<UnitResult<DesktopError>> ResetAsync() => face.ResetAsync();

    public Task<UnitResult<DesktopError>> StopAsync() => face.StopAsync();

    public Result<ImmutableArray<PackageArtifact>, DesktopError> PrepareArtifacts() =>
        face
            .Accepted.ToResult(
                new DesktopError("Face.Incomplete", "Accept the face before saving this person.")
            )
            .Bind(FacePackageArtifacts.Create);
}
