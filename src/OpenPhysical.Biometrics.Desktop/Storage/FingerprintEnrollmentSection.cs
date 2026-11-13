// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class FingerprintEnrollmentSection(
    FingerprintEnrollmentViewModel fingerprints,
    Maybe<CaptureViewModel> capture,
    Func<Result<TemplateMetadata, DesktopError>> metadata,
    Func<TemplateSaveOptions> options
) : EnrollmentSectionState(fingerprints), IEnrollmentSection
{
    private bool started;
    public string Id => "fingerprints";
    public string Title => "Fingerprints";
    public bool Complete => fingerprints.Complete;
    public int RequiredCount => fingerprints.TwoFingers ? 2 : 1;
    public int AcceptedCount => fingerprints.AcceptedCaptures.Length;
    public bool IsBusy => fingerprints.IsBusy;
    public bool IsEditing => fingerprints.Adjustment.IsOpen;
    public bool CanReset =>
        !IsBusy || IsEditing && fingerprints.Adjustment.CancelCommand.CanExecute(default);
    public string Progress => fingerprints.Progress;
    public string Instructions => fingerprints.Instructions;
    public ObservableObject ViewModel => fingerprints;
    public IAsyncRelayCommand AcceptCommand => fingerprints.AcceptCommand;
    public ImmutableArray<IAsyncRelayCommand> Commands =>
        [
            AcceptCommand,
            fingerprints.PrimarySlotCommand,
            fingerprints.SecondarySlotCommand,
            fingerprints.ResetCaptureCommand,
            fingerprints.AdjustCaptureCommand,
            fingerprints.Adjustment.ApplyCommand,
            fingerprints.Adjustment.CancelCommand,
        ];

    public async Task<UnitResult<DesktopError>> ActivateAsync(bool active)
    {
        if (capture.HasNoValue)
            return UnitResult.Success<DesktopError>();
        if (!active)
            return started
                ? await capture.Value.PauseSourceAsync()
                : UnitResult.Success<DesktopError>();
        if (started)
            return await capture.Value.ResumeSourceAsync();
        started = true;
        return await capture.Value.StartAsync(CancellationToken.None);
    }

    public Result<ImmutableArray<PackageArtifact>, DesktopError> PrepareArtifacts() =>
        fingerprints
            .Selection()
            .Bind(selected =>
                metadata()
                    .Bind(details =>
                        FingerprintPackageArtifacts.Create(selected, details, options())
                    )
            );

    public Task<UnitResult<DesktopError>> ResetAsync() => fingerprints.ResetAsync();

    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        fingerprints.BeginStop();
        var drained = await DesktopCommandTasks.DrainAsync(Commands.ToArray());
        var discarded = await fingerprints.Adjustment.DiscardAsync();
        fingerprints.ReleaseCaptures();
        var stopped = capture.HasValue
            ? await capture.Value.StopAsync()
            : UnitResult.Success<DesktopError>();
        return drained.IsFailure ? drained
            : discarded.IsFailure ? discarded
            : stopped;
    }
}
