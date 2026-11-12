// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Typed reference-view selection that remains valid when a document is replaced.</summary>
/// <param name="changed">Applies an actual selected reference view.</param>
public sealed class ReferenceSelectionViewModel(
    Func<ReferenceViewChoice, UnitResult<DesktopError>> changed
) : ObservableObject
{
    private ImmutableArray<ReferenceViewChoice> views = [];
    private ReferenceViewChoice selected = ReferenceViewChoice.Empty;
    private Maybe<(FingerPosition Finger, byte View)> preferred;

    /// <summary>Gets the actual views in the loaded reference.</summary>
    public ImmutableArray<ReferenceViewChoice> Views
    {
        get => views;
        private set => SetProperty(ref views, value);
    }

    /// <summary>Gets or sets the selected actual reference view.</summary>
    public ReferenceViewChoice SelectedView
    {
        get => selected;
        set
        {
            // Avalonia temporarily clears SelectedItem while replacing ItemsSource.
            if (value is null || !SetProperty(ref selected, value))
                return;
            Remember(value);
            changed(value);
        }
    }

    internal UnitResult<DesktopError> Replace(FingerprintRecord record)
    {
        var choices = record
            .Views.Select(
                (view, index) =>
                    new ReferenceViewChoice(
                        index,
                        (FingerPosition)view.FingerPosition,
                        view.ViewNumber
                    )
            )
            .ToImmutableArray();
        var chosen =
            choices.FirstOrDefault(choice =>
                preferred.HasValue
                && choice.Finger == preferred.Value.Finger
                && choice.ViewNumber == preferred.Value.View
            )
            ?? choices.FirstOrDefault()
            ?? ReferenceViewChoice.Empty;
        Views = choices;
        selected = chosen;
        Remember(chosen);
        OnPropertyChanged(nameof(SelectedView));
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Clear()
    {
        Views = [];
        selected = ReferenceViewChoice.Empty;
        OnPropertyChanged(nameof(SelectedView));
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Forget()
    {
        preferred = Maybe<(FingerPosition, byte)>.None;
        return Clear();
    }

    private UnitResult<DesktopError> Remember(ReferenceViewChoice choice)
    {
        if (choice.Index >= 0)
            preferred = (choice.Finger, choice.ViewNumber);
        return UnitResult.Success<DesktopError>();
    }
}
