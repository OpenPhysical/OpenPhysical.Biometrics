// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Coordinates accepting and saving one person and clearing the complete workspace.</summary>
public sealed class ExportViewModel : ObservableObject
{
    private readonly Maybe<InspectionViewModel> inspection;
    private string owner = EnrollmentWorkspaceReset.ProductOwner;
    private string productType = EnrollmentWorkspaceReset.Unreported;
    private string equipment = EnrollmentWorkspaceReset.Unreported;
    private string folder = TemplateStorage.DefaultFolder;
    private string status = string.Empty;
    private bool stopping;
    private bool includeDiagnostics;

    /// <summary>Connects enrollment to live capture, private storage and reference comparison.</summary>
    /// <param name="capture">Current capture and operator controls.</param>
    /// <param name="storage">Person-package writer.</param>
    /// <param name="suggestions">Suggested finger-plan provider.</param>
    /// <param name="inspection">Reference state cleared with the person.</param>
    public ExportViewModel(
        CaptureViewModel capture,
        ITemplateSessionStorage storage,
        EnrollmentFingerSuggestions suggestions,
        InspectionViewModel inspection
    )
        : this(
            () => capture.Captured,
            Maybe.From(capture),
            storage,
            suggestions,
            Maybe.From(inspection)
        ) { }

    internal ExportViewModel(
        Func<Maybe<CapturedFingerprint>> source,
        ITemplateSessionStorage storage,
        EnrollmentFingerSuggestions suggestions
    )
        : this(
            source,
            Maybe<CaptureViewModel>.None,
            storage,
            suggestions,
            Maybe<InspectionViewModel>.None
        ) { }

    private ExportViewModel(
        Func<Maybe<CapturedFingerprint>> source,
        Maybe<CaptureViewModel> live,
        ITemplateSessionStorage storage,
        EnrollmentFingerSuggestions suggestions,
        Maybe<InspectionViewModel> inspection
    )
    {
        this.inspection = inspection;
        Enrollment = new EnrollmentViewModel(
            source,
            live,
            suggestions,
            storage,
            Metadata,
            Maybe.From<Func<TemplateSaveOptions>>(() => new TemplateSaveOptions(IncludeDiagnostics))
        );
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !stopping && Ready);
        ClearPersonCommand = new AsyncRelayCommand(ClearPersonAsync, CanClearPerson);
        SaveCommand.PropertyChanged += (_, _) => ClearPersonCommand.NotifyCanExecuteChanged();
        Enrollment.PropertyChanged += (_, _) => Refresh();
    }

    /// <summary>Gets the current person and required captures.</summary>
    public EnrollmentViewModel Enrollment { get; }

    /// <summary>Gets or sets whether diagnostic contact planes are explicitly saved.</summary>
    public bool IncludeDiagnostics
    {
        get => includeDiagnostics;
        set => SetProperty(ref includeDiagnostics, value);
    }

    /// <summary>Gets or sets the CBEFF product owner in decimal or hexadecimal.</summary>
    public string ProductOwner
    {
        get => owner;
        set
        {
            if (SetProperty(ref owner, value))
                Refresh();
        }
    }

    /// <summary>Gets or sets the encoding product type.</summary>
    public string ProductType
    {
        get => productType;
        set
        {
            if (SetProperty(ref productType, value))
                Refresh();
        }
    }

    /// <summary>Gets or sets the capture equipment identifier.</summary>
    public string EquipmentId
    {
        get => equipment;
        set
        {
            if (SetProperty(ref equipment, value))
                Refresh();
        }
    }

    /// <summary>Gets or sets the persistent package destination.</summary>
    public string Folder
    {
        get => folder;
        set => SetProperty(ref folder, value);
    }

    /// <summary>Gets the latest save result or operator instruction.</summary>
    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    /// <summary>Gets whether every required capture and export field is ready.</summary>
    public bool Ready => Enrollment.Ready && Metadata().IsSuccess;

    /// <summary>Gets the next action required before saving.</summary>
    public string Readiness =>
        !Enrollment.Ready ? Enrollment.Instructions
        : Metadata().IsFailure ? Metadata().Error.Message
        : "Ready to save this person's fingerprints.";

    /// <summary>Gets the command that saves the accepted person artifacts.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Gets the command that clears captures, identity and comparison reference.</summary>
    public IAsyncRelayCommand ClearPersonCommand { get; }

    /// <summary>Cancels pending operations and releases unsaved capture references.</summary>
    /// <returns>Shutdown success or the operation failure.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopping = true;
        Enrollment.BeginStop();
        SaveCommand.Cancel();
        SaveCommand.NotifyCanExecuteChanged();
        ClearPersonCommand.NotifyCanExecuteChanged();
        var drained = await EnrollmentCommandTasks.DrainAsync(
            Enrollment,
            SaveCommand,
            ClearPersonCommand
        );
        var discarded = await Enrollment.Fingerprints.Adjustment.DiscardAsync();
        Enrollment.ReleaseCaptures();
        return drained.IsFailure ? drained : discarded;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (stopping)
            return;
        Status = "Saving this person's template and source images...";
        var saved = await Enrollment.WriteAsync(Folder, cancellationToken);
        if (!stopping)
            Status = saved.IsSuccess
                ? $"Saved: {string.Join(", ", saved.Value.Select(artifact => artifact.Path))}"
                : $"{saved.Error.Message} ({saved.Error.Code})";
    }

    private Result<TemplateMetadata, DesktopError> Metadata() =>
        TemplateMetadataParser.Parse(ProductOwner, ProductType, EquipmentId);

    private async Task ClearPersonAsync()
    {
        if (!CanClearPerson())
            return;
        var cleared = await EnrollmentWorkspaceReset.ApplyAsync(this, inspection);
        if (!stopping)
            Status = cleared.IsSuccess ? string.Empty : cleared.Error.Message;
    }

    private bool CanClearPerson() =>
        !stopping && !SaveCommand.IsRunning && EnrollmentCommandTasks.CanClear(Enrollment);

    private UnitResult<DesktopError> Refresh()
    {
        if (stopping)
            return UnitResult.Success<DesktopError>();
        OnPropertyChanged(nameof(Ready));
        OnPropertyChanged(nameof(Readiness));
        SaveCommand.NotifyCanExecuteChanged();
        ClearPersonCommand.NotifyCanExecuteChanged();
        return UnitResult.Success<DesktopError>();
    }
}
