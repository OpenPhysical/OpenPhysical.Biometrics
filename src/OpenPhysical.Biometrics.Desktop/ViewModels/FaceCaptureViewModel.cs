// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

/// <summary>Camera preview, source-bound crop review, and explicit facial acceptance for one person.</summary>
public sealed class FaceCaptureViewModel : ObservableObject
{
    private readonly FaceCaptureWorkflow workflow;
    private readonly FaceReviewOperations review;
    private readonly PersonEnrollmentViewModel person;
    private readonly FaceCaptureLifetime lifetime;
    private readonly FaceCameraSetupOperations setup;
    private readonly FaceCaptureNotifications notifications = new();

    /// <summary>Creates an attended facial workflow using shared person identity and reusable processing engines.</summary>
    /// <param name="cameras">Native camera provider.</param>
    /// <param name="portraits">Shared detection and source-supported portrait service.</param>
    /// <param name="records">Shared canonical facial record encoder.</param>
    /// <param name="person">The same person model used by fingerprints.</param>
    /// <param name="logger">Status-only application logger.</param>
    public FaceCaptureViewModel(
        ICameraProvider cameras,
        PortraitPreparer portraits,
        IFacialRecordPreparer records,
        PersonEnrollmentViewModel person,
        ILogger<FaceCaptureViewModel> logger
    )
    {
        this.person = person;
        _ = logger;
        workflow = new(cameras, portraits, records, Refresh, RequestAutomaticCapture);
        review = workflow.Review();
        setup = new(workflow, Refresh);
        Actions = new(workflow, review, setup.RefreshAsync, ResetAsync, CanAccept, () => IsBusy);
        Image = new(workflow.Display);
        lifetime = new(workflow, review, Actions);
        person.PropertyChanged += (_, _) => Refresh();
    }

    /// <summary>Gets cameras currently discovered by the platform.</summary>
    public ImmutableArray<CameraSource> Sources => workflow.Selection.Sources;

    /// <summary>Gets or selects the actual camera while no capture is pending.</summary>
    public CameraSource SelectedSource
    {
        get => workflow.Selection.Source;
        set => setup.Select(value);
    }

    /// <summary>Gets native modes for the selected camera.</summary>
    public ImmutableArray<CameraMode> Modes => SelectedSource.Modes;

    /// <summary>Gets or selects a native, unresampled camera mode.</summary>
    public CameraMode SelectedMode
    {
        get => workflow.Selection.Mode;
        set => setup.Select(value);
    }

    /// <summary>Gets whether camera setup evidence is required for this exact device and mode.</summary>
    public bool QualificationRequired => !workflow.Selection.Qualified;

    /// <summary>Gets the facial images and their independently controlled cached guides.</summary>
    public FaceImageViewModel Image { get; }

    /// <summary>Gets whether preview acquisition is active.</summary>
    public bool IsLive => workflow.IsLive;

    /// <summary>Gets whether a ready face is captured automatically after its settling interval.</summary>
    public bool AutoCapture => workflow.Automatic.Enabled;

    /// <summary>Gets whether current image processing is in progress.</summary>
    public bool IsBusy => workflow.Busy || setup.Busy;

    /// <summary>Gets whether a frozen image awaits review or is accepted.</summary>
    public bool HasCapture => workflow.Draft.Source.HasValue;

    /// <summary>Gets whether the face has been explicitly accepted.</summary>
    public bool Complete => workflow.Draft.Accepted.HasValue;

    /// <summary>Gets whether a frozen face awaits an explicit review or acceptance action.</summary>
    public bool IsReviewing => HasCapture && !Complete && !RequiresRetake;

    /// <summary>Gets whether a new image is needed instead of crop adjustment or acceptance.</summary>
    public bool RequiresRetake => workflow.Draft.RequiresRetake;

    /// <summary>Gets whether an explicit override is available for the current supported portrait.</summary>
    public bool CanOverride => review.CanOverride;

    /// <summary>Gets whether automatic capture tests failed, including recorded overrides.</summary>
    public bool HasFailures => FailureMessage.Length > 0;

    /// <summary>Gets human-readable failed test names without erasing acknowledged failures.</summary>
    public string FailureMessage => FaceRetakeGuidance.Message(workflow.Draft.Portrait);

    /// <summary>Gets whether reset can cancel current review safely.</summary>
    public bool CanReset => !IsBusy;

    /// <summary>Gets accepted facial object progress.</summary>
    public string Progress => FacePresentation.Progress(workflow);

    /// <summary>Gets one next operator instruction.</summary>
    public string Instructions => FacePresentation.Instructions(workflow, person);

    /// <summary>Gets advisory measurements and native mode information for Details.</summary>
    public string Details => FacePresentation.Details(workflow.Draft, !QualificationRequired);

    /// <summary>Gets or sets explicit operator review of the portrait, lighting, eyes and background.</summary>
    public bool ReviewConfirmed
    {
        get => workflow.Draft.ReviewConfirmed;
        set => review.Confirm(value);
    }

    /// <summary>Gets the actions rendered by the facial view.</summary>
    public FaceCaptureCommands Actions { get; }

    /// <summary>Gets commands drained with the person session.</summary>
    public ImmutableArray<IAsyncRelayCommand> Commands => Actions.All;

    /// <summary>Gets explicit facial acceptance and single encoding.</summary>
    public IAsyncRelayCommand AcceptCommand => Actions.Accept;

    /// <summary>Revokes confirmation or preserves an existing measured receipt for the actual device and mode.</summary>
    /// <param name="sourceId">The actual selected camera identity.</param>
    /// <param name="mode">The exact native mode qualified during camera setup.</param>
    /// <param name="confirmed">False revokes confirmation; true cannot create proof without an existing receipt.</param>
    /// <returns>Qualification association success or a source/mode mismatch.</returns>
    public UnitResult<DesktopError> OpticalQualificationApplied(
        string sourceId,
        CameraMode mode,
        bool confirmed
    ) => workflow.Selection.Qualify(sourceId, mode, confirmed).Bind(Refresh);

    /// <summary>Associates an actual optical measurement with its exact selected device and native mode.</summary>
    /// <param name="receipt">Immutable operator measurement, retained with accepted capture evidence.</param>
    /// <returns>Receipt validation and association outcome.</returns>
    public UnitResult<DesktopError> OpticalQualificationApplied(
        CameraOpticalQualification receipt
    ) => workflow.Selection.Qualify(receipt).Bind(Refresh);

    /// <summary>Starts or pauses the camera without discarding captured person data.</summary>
    /// <param name="active">Whether this is the active enrollment section.</param>
    /// <returns>Camera lifecycle outcome.</returns>
    public Task<UnitResult<DesktopError>> ActivateAsync(bool active) =>
        lifetime.ActivateAsync(active);

    /// <summary>Clears all facial pixels and accepted data for the person.</summary>
    /// <returns>Reset and camera restart outcome.</returns>
    public Task<UnitResult<DesktopError>> ResetAsync() => lifetime.ResetAsync();

    /// <summary>Rechecks an unaccepted photo with the selected mouth tolerance, retaining its pixels.</summary>
    /// <param name="policy">Fixed application tolerance for current drafts and later captures.</param>
    /// <returns>The source-bound reassessment outcome.</returns>
    public Task<UnitResult<DesktopError>> ConfigureMouthPolicyAsync(PortraitMouthPolicy policy) =>
        lifetime.ConfigureMouthPolicyAsync(policy);

    /// <summary>Applies automatic photo capture without changing an existing frozen photo.</summary>
    /// <param name="enabled">Whether ready live photos are captured automatically.</param>
    /// <param name="delay">Continuous readiness interval, from zero to ten seconds.</param>
    /// <returns>Validated configuration application.</returns>
    public Task<UnitResult<DesktopError>> ConfigureAutoCaptureAsync(bool enabled, TimeSpan delay) =>
        Task.FromResult(workflow.Automatic.Configure(enabled, delay).Bind(Refresh));

    /// <summary>Changes saved-photo background replacement, reusing frozen source detections and masks.</summary>
    /// <param name="enabled">Whether to replace confidently identified background.</param>
    /// <returns>The current source-bound reprocessing result.</returns>
    public Task<UnitResult<DesktopError>> ConfigureBackgroundAsync(bool enabled) =>
        lifetime.ConfigureBackgroundAsync(enabled);

    /// <summary>Drains commands and releases the camera and all person data.</summary>
    /// <returns>Drain and release outcome.</returns>
    public Task<UnitResult<DesktopError>> StopAsync() => lifetime.StopAsync();

    /// <summary>Gets the canonical accepted object and cropped-image evidence, without raw camera pixels.</summary>
    public Maybe<FaceAcceptedCapture> Accepted => workflow.Draft.Accepted;

    private bool CanAccept() => review.CanAccept(person.Create().IsSuccess);

    private UnitResult<DesktopError> RequestAutomaticCapture() => Actions.RequestAutomaticCapture();

    private UnitResult<DesktopError> Refresh() =>
        notifications.Refresh(workflow, Image, Commands, OnPropertyChanged);
}
