// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

/// <summary>Edits validated image adjustment choices.</summary>
public sealed class ImageAdjustmentViewModel : ObservableObject
{
    private ImageAdjustmentSettings value = ImageAdjustmentSettings.Default;

    /// <summary>Creates image controls with Original and Reset commands.</summary>
    public ImageAdjustmentViewModel()
    {
        OriginalCommand = new RelayCommand(() => Set(ImageAdjustmentSettings.Original));
        ResetCommand = new RelayCommand(() => Set(ImageAdjustmentSettings.Default));
    }

    /// <summary>Occurs after valid image controls change.</summary>
    public event Action<ImageAdjustmentSettings> Changed = _ => { };

    /// <summary>Gets current validated image controls.</summary>
    public ImageAdjustmentSettings Value => value;

    /// <summary>Gets the command that selects exact original pixels.</summary>
    public IRelayCommand OriginalCommand { get; }

    /// <summary>Gets the command that restores initial image controls.</summary>
    public IRelayCommand ResetCommand { get; }

    /// <summary>Gets or sets automatic local enhancement.</summary>
    public bool AutomaticEnhancement
    {
        get => value.AutomaticEnhancement;
        set => Update(value, Brightness, Contrast);
    }

    /// <summary>Gets or sets brightness.</summary>
    public int Brightness
    {
        get => value.Brightness;
        set => Update(AutomaticEnhancement, value, Contrast);
    }

    /// <summary>Gets or sets contrast.</summary>
    public int Contrast
    {
        get => value.Contrast;
        set => Update(AutomaticEnhancement, Brightness, value);
    }

    /// <summary>Restores validated image controls.</summary>
    /// <param name="settings">Validated image controls.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public UnitResult<DesktopError> Restore(ImageAdjustmentSettings settings) =>
        settings is not null
            ? Set(settings)
            : new DesktopError("Settings.ImageAdjustment", "Image controls are required.");

    private UnitResult<DesktopError> Update(bool automatic, int brightness, int contrast) =>
        ImageAdjustmentSettings.Create(automatic, brightness, contrast).Bind(Set);

    private UnitResult<DesktopError> Set(ImageAdjustmentSettings settings)
    {
        if (value == settings)
            return UnitResult.Success<DesktopError>();
        value = settings;
        OnPropertyChanged(nameof(AutomaticEnhancement));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Contrast));
        Changed(value);
        return UnitResult.Success<DesktopError>();
    }
}
