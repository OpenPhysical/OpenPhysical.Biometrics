// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Edits validated advanced capture choices while preserving unrelated controls.</summary>
public sealed class CapturePolicyViewModel : ObservableObject
{
    private CapturePolicySettings value = CapturePolicySettings.Default;

    /// <summary>Occurs after valid capture choices change.</summary>
    public event Action<CapturePolicySettings> Changed = _ => { };

    /// <summary>Gets the current validated capture choices.</summary>
    public CapturePolicySettings Value => value;

    /// <summary>Gets or sets the enhancement blend percentage.</summary>
    public int EnhancementPercent
    {
        get => (int)Math.Round(value.Enhancement.Blend * 100);
        set =>
            Update(
                EnhancementSettings
                    .Create(
                        this.value.Enhancement.TileSize,
                        this.value.Enhancement.ClipLimit,
                        value / 100d
                    )
                    .Bind(enhancement =>
                        Create(
                            enhancement,
                            this.value.MinimumNfiq2Gain,
                            this.value.EvidenceOptions.InnerGuard,
                            this.value.ContextMargin
                        )
                    )
            );
    }

    /// <summary>Gets or sets the required NFIQ2 improvement.</summary>
    public int MinimumQualityGain
    {
        get => value.MinimumNfiq2Gain;
        set =>
            Update(
                Create(
                    this.value.Enhancement,
                    value,
                    this.value.EvidenceOptions.InnerGuard,
                    this.value.ContextMargin
                )
            );
    }

    /// <summary>Gets or sets image context, which never expands feature admission.</summary>
    public int ContextMargin
    {
        get => value.ContextMargin;
        set =>
            Update(
                Create(
                    this.value.Enhancement,
                    this.value.MinimumNfiq2Gain,
                    this.value.EvidenceOptions.InnerGuard,
                    value
                )
            );
    }

    /// <summary>Gets or sets the existing modified-edge guard.</summary>
    public int EdgeGuardPixels
    {
        get => value.EvidenceOptions.InnerGuard;
        set =>
            Update(
                Create(
                    this.value.Enhancement,
                    this.value.MinimumNfiq2Gain,
                    value,
                    this.value.ContextMargin
                )
            );
    }

    /// <summary>Restores validated capture choices.</summary>
    /// <param name="settings">Validated capture configuration.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public UnitResult<DesktopError> Restore(CapturePolicySettings settings)
    {
        if (settings is null)
            return UnitResult.Failure(
                new DesktopError("Settings.Policy", "Capture policy is required.")
            );
        if (value == settings)
            return UnitResult.Success<DesktopError>();
        value = settings;
        OnPropertyChanged(nameof(EnhancementPercent));
        OnPropertyChanged(nameof(MinimumQualityGain));
        OnPropertyChanged(nameof(EdgeGuardPixels));
        OnPropertyChanged(nameof(ContextMargin));
        OnPropertyChanged(nameof(WholeContactWindowSize));
        OnPropertyChanged(nameof(WholeContactGridStep));
        OnPropertyChanged(nameof(MinimumUncorroboratedArea));
        Changed(value);
        return UnitResult.Success<DesktopError>();
    }

    private Result<CapturePolicySettings, DesktopError> Create(
        EnhancementSettings enhancement,
        int gain,
        int guard,
        int margin
    ) =>
        CapturePolicyChange.Apply(
            value,
            enhancement,
            value.EvidenceOptions with
            {
                InnerGuard = guard,
            },
            gain,
            margin
        );

    /// <summary>Gets or sets the C-only qualification window side length.</summary>
    public int WholeContactWindowSize
    {
        get => value.WholeContactOptions.WindowSize;
        set =>
            Update(
                this.value.WithWholeContact(
                    this.value.WholeContactOptions with
                    {
                        WindowSize = value,
                    }
                )
            );
    }

    /// <summary>Gets or sets the fixed qualification grid spacing.</summary>
    public int WholeContactGridStep
    {
        get => value.WholeContactOptions.GridStep;
        set =>
            Update(
                this.value.WithWholeContact(
                    this.value.WholeContactOptions with
                    {
                        GridStep = value,
                    }
                )
            );
    }

    /// <summary>Gets or sets independent exterior uncertainty area without finger aspect constraints.</summary>
    public int MinimumUncorroboratedArea
    {
        get => value.WholeContactOptions.MinimumUncorroboratedArea;
        set =>
            Update(
                this.value.WithWholeContact(
                    this.value.WholeContactOptions with
                    {
                        MinimumUncorroboratedArea = value,
                    }
                )
            );
    }

    private UnitResult<DesktopError> Update(Result<CapturePolicySettings, DesktopError> settings) =>
        settings.Bind(Restore);
}
