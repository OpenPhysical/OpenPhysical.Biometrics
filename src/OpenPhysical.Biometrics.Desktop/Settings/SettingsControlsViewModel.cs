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
    private bool automatic = true;
    private bool invert;
    private string status = "Changes apply immediately. Save to keep them for the next session.";

    /// <summary>Gets or sets whether settings changes have stopped.</summary>
    protected bool Stopping { get; set; }

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

    /// <summary>Gets or sets automatic capture.</summary>
    public bool AutoCapture
    {
        get => automatic;
        set
        {
            if (!Stopping && SetProperty(ref automatic, value))
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

    /// <summary>Applies a form change through the configuration owner.</summary>
    /// <returns>Application success or a validation failure.</returns>
    protected abstract UnitResult<DesktopError> Changed();

    /// <summary>Restores form controls without applying intermediate values.</summary>
    /// <param name="preferences">Validated configuration.</param>
    /// <returns>Successful restoration.</returns>
    protected UnitResult<DesktopError> RestoreControls(DesktopPreferences preferences)
    {
        threshold = preferences.ComparisonThreshold.ToString(CultureInfo.InvariantCulture);
        delay = preferences.AutoCaptureDelaySeconds.ToString(CultureInfo.InvariantCulture);
        automatic = preferences.AutoCapture;
        invert = preferences.InvertImage;
        OnPropertyChanged(nameof(Threshold));
        OnPropertyChanged(nameof(AutoCaptureDelaySeconds));
        OnPropertyChanged(nameof(AutoCapture));
        OnPropertyChanged(nameof(Invert));
        return UnitResult.Success<DesktopError>();
    }
}
