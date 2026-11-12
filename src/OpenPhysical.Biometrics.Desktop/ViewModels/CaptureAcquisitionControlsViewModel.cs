// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Exposes the operator finger and sensor exposure controls.</summary>
public abstract class CaptureAcquisitionControlsViewModel : ObservableObject
{
    private protected CaptureAcquisitionControlsViewModel() { }

    private FingerChoice selectedFinger = FingerChoice.All.First(item =>
        item.Position == FingerPosition.RightIndex
    );
    private double exposure = DesktopCaptureDefaults.Exposure;
    private bool automaticExposure = true;

    /// <summary>Gets selectable finger identities.</summary>
    public ImmutableArray<FingerChoice> Fingers => FingerChoice.All;

    /// <summary>Gets or sets the finger assigned to the next capture.</summary>
    public FingerChoice SelectedFinger
    {
        get => selectedFinger;
        set
        {
            if (value is not null)
                SetProperty(ref selectedFinger, value);
        }
    }

    /// <summary>Gets or sets the requested sensor exposure.</summary>
    public double Exposure
    {
        get => exposure;
        set => SetProperty(ref exposure, value);
    }

    /// <summary>Gets or sets whether valid contact can adjust exposure.</summary>
    public bool AutomaticExposure
    {
        get => automaticExposure;
        set => SetProperty(ref automaticExposure, value);
    }
}
