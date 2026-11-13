// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Records optical evidence for the exact selected camera and native mode.</summary>
public sealed class CameraSetupViewModel : ObservableObject
{
    private readonly FaceCaptureViewModel face;
    private readonly CameraQualificationStorage storage;
    private ImmutableArray<CameraOpticalQualification> qualifications = [];
    private string sourceId = string.Empty;
    private Maybe<OpenPhysical.Biometrics.Capture.Camera.CameraMode> mode;
    private string operatorId = string.Empty;
    private decimal targetPixels;
    private bool targetConfirmed;
    private string status = string.Empty;

    /// <summary>Creates setup controls bound to the actual camera selection.</summary>
    /// <param name="face">The camera workspace.</param>
    /// <param name="storage">Private non-biometric setup storage.</param>
    public CameraSetupViewModel(FaceCaptureViewModel face, CameraQualificationStorage storage)
    {
        this.face = face;
        this.storage = storage;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        ForgetCommand = new AsyncRelayCommand(ForgetAsync);
        face.PropertyChanged += (_, args) =>
        {
            if (
                args.PropertyName
                is nameof(FaceCaptureViewModel.SelectedSource)
                    or nameof(FaceCaptureViewModel.SelectedMode)
            )
                SelectionChanged();
        };
    }

    /// <summary>Gets the selected camera and native mode.</summary>
    public string Selection => $"{face.SelectedSource.Name} · {face.SelectedMode}";

    /// <summary>Gets or sets the operator-provided identifier attached to the measurement.</summary>
    public string OperatorId
    {
        get => operatorId;
        set
        {
            SetProperty(ref operatorId, value);
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Gets or sets the measured target width in source pixels.</summary>
    public decimal TargetPixels
    {
        get => targetPixels;
        set
        {
            SetProperty(ref targetPixels, value);
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Gets or sets confirmation of the physical target size, distance and unchanged camera zoom.</summary>
    public bool TargetConfirmed
    {
        get => targetConfirmed;
        set
        {
            SetProperty(ref targetConfirmed, value);
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Gets the setup outcome without captured image data.</summary>
    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    /// <summary>Gets explicit measurement recording.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Gets revocation for the selected device and mode.</summary>
    public IAsyncRelayCommand ForgetCommand { get; }

    /// <summary>Loads previously reviewed measurements before starting acquisition.</summary>
    /// <returns>The validated configuration outcome.</returns>
    public async Task<UnitResult<DesktopError>> InitializeAsync()
    {
        var loaded = await storage.LoadAsync();
        if (loaded.IsFailure)
        {
            Status = loaded.Error.Message;
            return loaded.Error;
        }
        qualifications = loaded.Value;
        SelectionChanged();
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Gets verified evidence associated with the selected actual camera mode.</summary>
    public Maybe<CameraOpticalQualification> Current =>
        qualifications.Where(Matches).Select(Maybe.From).FirstOrDefault();

    private bool CanSave() =>
        face.SelectedSource.Id.Length > 0
        && face.SelectedMode.Width > 420
        && TargetConfirmed
        && TargetPixels >= 240
        && !string.IsNullOrWhiteSpace(OperatorId);

    private async Task SaveAsync()
    {
        if (!CanSave())
            return;
        var value = new CameraOpticalQualification(
            face.SelectedSource.Id,
            face.SelectedMode,
            (double)TargetPixels,
            DateTimeOffset.UtcNow,
            OperatorId.Trim()
        );
        var next = qualifications.Where(item => !Matches(item)).Append(value).ToImmutableArray();
        var saved = await storage.SaveAsync(next);
        if (saved.IsFailure)
        {
            Status = saved.Error.Message;
            return;
        }
        qualifications = next;
        face.OpticalQualificationApplied(value);
        Status = "Optical measurement recorded for this camera and mode.";
    }

    private async Task ForgetAsync()
    {
        var next = qualifications.Where(item => !Matches(item)).ToImmutableArray();
        var saved = await storage.SaveAsync(next);
        if (saved.IsFailure)
        {
            Status = saved.Error.Message;
            return;
        }
        qualifications = next;
        face.OpticalQualificationApplied(face.SelectedSource.Id, face.SelectedMode, false);
        Status = "Camera measurement removed.";
    }

    private bool Matches(CameraOpticalQualification value) =>
        value.SourceId == face.SelectedSource.Id && value.Mode == face.SelectedMode;

    private UnitResult<DesktopError> SelectionChanged()
    {
        OnPropertyChanged(nameof(Selection));
        if (
            sourceId == face.SelectedSource.Id
            && mode.Match(value => value == face.SelectedMode, () => false)
        )
            return UnitResult.Success<DesktopError>();
        sourceId = face.SelectedSource.Id;
        mode = face.SelectedMode;
        var saved = qualifications.Where(Matches).Select(Maybe.From).FirstOrDefault();
        TargetPixels = saved.HasValue ? (decimal)saved.Value.TargetPixels : 0;
        TargetConfirmed = false;
        if (saved.HasValue)
            OperatorId = saved.Value.OperatorId;
        return saved.HasValue
            ? face.OpticalQualificationApplied(saved.Value)
            : face.OpticalQualificationApplied(face.SelectedSource.Id, face.SelectedMode, false);
    }
}
