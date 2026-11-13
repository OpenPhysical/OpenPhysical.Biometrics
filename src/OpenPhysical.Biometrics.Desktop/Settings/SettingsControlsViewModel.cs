// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

/// <summary>Editable settings form state, separated from persistence commands.</summary>
public abstract class SettingsControlsViewModel : ObservableObject
{
    private string threshold = "40";
    private string delay = "1";
    private string mouthTolerance = "4";
    private bool automatic;
    private bool automaticPhoto;
    private bool replacePhotoBackground = true;
    private bool invert;
    private string status = "Changes apply immediately. Save to keep them for the next session.";
    private Maybe<SettingsNotice> notice;

    /// <summary>Gets or sets whether settings changes have stopped.</summary>
    protected bool Stopping { get; set; }

    /// <summary>Gets image adjustment form controls.</summary>
    public ImageAdjustmentViewModel ImageAdjustments { get; } = new();

    /// <summary>Gets advanced fingerprint capture form controls.</summary>
    public CapturePolicyViewModel CapturePolicy { get; } = new();

    /// <summary>Gets or sets context margin without changing feature admission.</summary>
    public int ContextMargin
    {
        get => CapturePolicy.ContextMargin;
        set => CapturePolicy.ContextMargin = value;
    }

    /// <summary>Gets or sets the editable comparison threshold.</summary>
    public string Threshold
    {
        get => threshold;
        set
        {
            if (!Stopping && SetProperty(ref threshold, value))
                Changed();
        }
    }

    /// <summary>Gets or sets the editable presentation delay.</summary>
    public string AutoCaptureDelaySeconds
    {
        get => delay;
        set
        {
            if (!Stopping && SetProperty(ref delay, value))
                Changed();
        }
    }

    /// <summary>Gets or sets the permitted lip gap as a percentage of mouth width.</summary>
    public string MouthTolerancePercent
    {
        get => mouthTolerance;
        set
        {
            if (!Stopping && SetProperty(ref mouthTolerance, value))
                Changed();
        }
    }

    /// <summary>Gets or sets automatic fingerprint capture.</summary>
    public bool AutoCapture
    {
        get => automatic;
        set
        {
            if (!Stopping && SetProperty(ref automatic, value))
                Changed();
        }
    }

    /// <summary>Gets or sets automatic photo capture independently of fingerprints.</summary>
    public bool FaceAutoCapture
    {
        get => automaticPhoto;
        set
        {
            if (!Stopping && SetProperty(ref automaticPhoto, value))
                Changed();
        }
    }

    /// <summary>Gets or sets whether captured photos replace the segmented background.</summary>
    public bool FaceBackgroundReplacement
    {
        get => replacePhotoBackground;
        set
        {
            if (!Stopping && SetProperty(ref replacePhotoBackground, value))
                Changed();
        }
    }

    /// <summary>Gets or sets display inversion.</summary>
    public bool Invert
    {
        get => invert;
        set
        {
            if (!Stopping && SetProperty(ref invert, value))
                Changed();
        }
    }

    /// <summary>Gets the current configuration status.</summary>
    public string Status
    {
        get => status;
        protected set => SetProperty(ref status, value);
    }

    /// <summary>Gets a pending transient notice, consumed by the window presenter.</summary>
    public Maybe<SettingsNotice> Notice
    {
        get => notice;
        protected set => SetProperty(ref notice, value);
    }

    /// <summary>Consumes the transient notice without changing settings or status.</summary>
    /// <returns>Successful acknowledgement.</returns>
    public UnitResult<DesktopError> DismissNotice()
    {
        Notice = default;
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Applies a form change through the configuration owner.</summary>
    /// <returns>Application success or a validation failure.</returns>
    protected abstract UnitResult<DesktopError> Changed();

    /// <summary>Reads the current form as validated immutable preferences.</summary>
    /// <returns>Validated preferences or the invalid control's error.</returns>
    protected Result<DesktopPreferences, DesktopError> Preferences() =>
        SettingsInput
            .Parse(Threshold, AutoCaptureDelaySeconds, MouthTolerancePercent, AutoCapture, Invert)
            .Map(WithPolicies);

    /// <summary>Attaches the validated form policies to primary controls.</summary>
    /// <param name="value">Primary controls and mouth policy.</param>
    /// <returns>The complete immutable preferences.</returns>
    protected DesktopPreferences WithPolicies(DesktopPreferences value) =>
        value with
        {
            ImageAdjustments = ImageAdjustments.Value,
            CapturePolicy = CapturePolicy.Value,
            FaceAutoCapture = FaceAutoCapture,
            FaceBackgroundReplacement = FaceBackgroundReplacement,
        };

    /// <summary>Restores form controls without applying intermediate values.</summary>
    /// <param name="preferences">Validated configuration.</param>
    /// <returns>Successful restoration.</returns>
    protected UnitResult<DesktopError> RestoreControls(DesktopPreferences preferences)
    {
        threshold = preferences.ComparisonThreshold.ToString(CultureInfo.InvariantCulture);
        delay = preferences.AutoCaptureDelaySeconds.ToString(CultureInfo.InvariantCulture);
        mouthTolerance = (preferences.MouthPolicy.MaximumOpeningRatio * 100).ToString(
            "0.########",
            CultureInfo.InvariantCulture
        );
        automatic = preferences.AutoCapture;
        automaticPhoto = preferences.FaceAutoCapture;
        replacePhotoBackground = preferences.FaceBackgroundReplacement;
        invert = preferences.InvertImage;
        OnPropertyChanged(nameof(Threshold));
        OnPropertyChanged(nameof(AutoCaptureDelaySeconds));
        OnPropertyChanged(nameof(MouthTolerancePercent));
        OnPropertyChanged(nameof(AutoCapture));
        OnPropertyChanged(nameof(FaceAutoCapture));
        OnPropertyChanged(nameof(FaceBackgroundReplacement));
        OnPropertyChanged(nameof(Invert));
        return UnitResult.Success<DesktopError>();
    }
}
