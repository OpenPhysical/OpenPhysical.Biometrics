// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Coordinates accepting and saving one person and clearing the complete workspace.</summary>
public sealed class ExportViewModel : EnrollmentExportOptions
{
    private readonly Maybe<InspectionViewModel> inspection;
    private string status = string.Empty;
    private bool stopping;
    private readonly IEnrollmentPackageStorage storage;
    private readonly DesktopFilePicker picker;
    private readonly Maybe<CameraSetupViewModel> cameraSetup;
    private Maybe<BiometricPackage> openedPackage;
    private string personId = Guid.NewGuid().ToString("N");

    /// <summary>Connects registered capture sections to one encrypted person-package store.</summary>
    /// <param name="capture">Fingerprint acquisition and review.</param>
    /// <param name="storage">Whole-person encrypted storage.</param>
    /// <param name="suggestions">Stable opposite-hand finger suggestions.</param>
    /// <param name="inspection">Comparison state cleared with the person.</param>
    /// <param name="person">Identity shared by all modalities.</param>
    /// <param name="additional">Registered non-fingerprint capture sections.</param>
    /// <param name="protection">Transient package passphrase owner.</param>
    /// <param name="picker">Local encrypted package picker.</param>
    /// <param name="cameraSetup">Optional optical acquisition evidence.</param>
    public ExportViewModel(
        CaptureViewModel capture,
        IEnrollmentPackageStorage storage,
        EnrollmentFingerSuggestions suggestions,
        InspectionViewModel inspection,
        PersonEnrollmentViewModel person,
        System.Collections.Immutable.ImmutableArray<IEnrollmentSection> additional,
        PackageProtectionViewModel protection,
        DesktopFilePicker picker,
        Maybe<CameraSetupViewModel> cameraSetup
    )
    {
        this.storage = storage;
        this.cameraSetup = cameraSetup;
        this.picker = picker;
        this.inspection = inspection;
        Protection = protection;
        var fingerSection = EnrollmentSectionRegistration.Fingerprints(
            capture,
            suggestions,
            person,
            Metadata,
            () => new(IncludeDiagnostics)
        );
        Enrollment = EnrollmentViewModel.Create(person, [.. additional, fingerSection]).Value;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !stopping && Ready);
        OpenCommand = new AsyncRelayCommand(
            OpenAsync,
            () => !stopping && !SaveCommand.IsRunning && !Enrollment.IsEditing
        );
        ClearPersonCommand = new AsyncRelayCommand(ClearPersonAsync, CanClearPerson);
        SaveCommand.PropertyChanged += (_, _) => Refresh();
        Enrollment.PropertyChanged += (_, _) => Refresh();
        ExportProtectionBinding.Attach(this);
    }

    /// <summary>Gets optional camera setup content for the settings presenter.</summary>
    public object CameraSetupContent =>
        cameraSetup.Match(value => (object)value, () => (object)string.Empty);

    /// <summary>Gets in-memory package protection, independent of person-specific state.</summary>
    public PackageProtectionViewModel Protection { get; }

    /// <summary>Gets the command opening an authenticated encrypted person package.</summary>
    public IAsyncRelayCommand OpenCommand { get; }

    /// <summary>Gets the last opened package for explicit local inspection.</summary>
    public Maybe<BiometricPackage> OpenedPackage
    {
        get => openedPackage;
        private set => SetProperty(ref openedPackage, value);
    }

    /// <summary>Gets the current person and required captures.</summary>
    public EnrollmentViewModel Enrollment { get; }

    /// <summary>Gets the latest save result or operator instruction.</summary>
    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    /// <summary>Gets whether every required capture and export field is ready.</summary>
    public bool Ready => Enrollment.Ready && (!HasFingerprints || Metadata().IsSuccess);

    private bool HasFingerprints => Enrollment.Sections.Any(s => s.Id == "fingerprints");

    /// <summary>Gets the next action required before saving.</summary>
    public string Readiness =>
        !Enrollment.Ready ? Enrollment.Instructions
        : HasFingerprints && Metadata().IsFailure ? Metadata().Error.Message
        : "Ready to save this person's encrypted package.";

    /// <summary>Gets the command that saves the accepted person artifacts.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Gets the command that clears captures, identity and comparison reference.</summary>
    public IAsyncRelayCommand ClearPersonCommand { get; }

    /// <summary>Prepares the accepted unsigned objects in memory using the package pipeline.</summary>
    /// <returns>An immutable snapshot or the precise missing acceptance or metadata error.</returns>
    public Result<AcceptedBiometricSnapshot, DesktopError> PrepareAcceptedPackage() =>
        stopping
            ? new DesktopError("Session.Closed", "The person session has ended.")
            : AcceptedBiometricSnapshots.Prepare(this, personId);

    /// <summary>Checks that a retained snapshot still belongs to the current person and privacy generation.</summary>
    /// <param name="snapshot">The exact snapshot retained before asynchronous preparation.</param>
    /// <returns>Success only while identity, captures and privacy generation remain current.</returns>
    public UnitResult<DesktopError> ValidateAcceptedPackage(AcceptedBiometricSnapshot snapshot) =>
        stopping
            ? new DesktopError("Session.Closed", "The person session has ended.")
            : AcceptedBiometricSnapshots.Validate(this, personId, snapshot);

    /// <summary>Cancels pending operations and releases unsaved capture references.</summary>
    /// <returns>Shutdown success or the operation failure.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopping = true;
        Protection.Dispose();
        SaveCommand.Cancel();
        OpenCommand.Cancel();
        Refresh();
        var drained = await DesktopCommandTasks.DrainAsync(
            SaveCommand,
            OpenCommand,
            ClearPersonCommand
        );
        var stopped = await Enrollment.StopAsync();
        OpenedPackage = Maybe<BiometricPackage>.None;
        return drained.IsFailure ? drained : stopped;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (stopping)
            return;
        Status = "Saving encrypted person package…";
        var saved = await PersonPackageWriter.SaveAsync(this, storage, personId, cancellationToken);
        if (!stopping)
            Status = saved.IsSuccess
                ? $"Saved: {saved.Value}"
                : $"{saved.Error.Message} ({saved.Error.Code})";
    }

    private async Task OpenAsync(CancellationToken token)
    {
        if (stopping)
            return;
        ForgetOpenedPackage();
        var opened = await PersonPackageReader.OpenAsync(picker, Protection, storage, token);
        if (stopping)
            return;
        OpenedPackage = opened.IsSuccess ? opened.Value : Maybe<BiometricPackage>.None;
        Status = opened.IsSuccess ? "Encrypted person package opened." : opened.Error.Message;
    }

    internal UnitResult<DesktopError> ForgetOpenedPackage()
    {
        OpenedPackage = Maybe<BiometricPackage>.None;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Report(DesktopError error)
    {
        if (!stopping)
            Status = error.Message;
        return UnitResult.Success<DesktopError>();
    }

    /// <inheritdoc/>
    protected override UnitResult<DesktopError> OptionsChanged() => Refresh();

    private async Task ClearPersonAsync()
    {
        if (!CanClearPerson())
            return;
        var cleared = await EnrollmentWorkspaceReset.ApplyAsync(this, inspection);
        personId = Guid.NewGuid().ToString("N");
        OpenedPackage = Maybe<BiometricPackage>.None;
        if (!stopping)
            Status = cleared.IsSuccess ? string.Empty : cleared.Error.Message;
    }

    private bool CanClearPerson() =>
        !stopping && !SaveCommand.IsRunning && !OpenCommand.IsRunning && Enrollment.CanReset;

    private UnitResult<DesktopError> Refresh() =>
        stopping
            ? UnitResult.Success<DesktopError>()
            : DesktopCommandNotifications.Refresh(
                name => OnPropertyChanged(name),
                [nameof(Ready), nameof(Readiness)],
                SaveCommand,
                ClearPersonCommand,
                OpenCommand
            );
}
