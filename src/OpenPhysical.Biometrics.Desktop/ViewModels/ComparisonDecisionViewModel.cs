// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Read-only match decision state for the current comparison inputs.</summary>
public sealed class ComparisonDecisionViewModel : ObservableObject
{
    private Maybe<bool> accepted;

    /// <summary>Gets the current decision, absent until comparison completes.</summary>
    public Maybe<bool> Accepted => accepted;

    /// <summary>Gets whether a current decision exists.</summary>
    public bool HasDecision => Accepted.HasValue;

    /// <summary>Gets whether the current decision is a match.</summary>
    public bool IsMatch => Accepted.Match(value => value, () => false);

    /// <summary>Gets whether the current decision is a nonmatch.</summary>
    public bool IsNoMatch => Accepted.Match(value => !value, () => false);

    /// <summary>Gets the current decision label, empty before a decision.</summary>
    public string Label =>
        Accepted.Match(value => value ? "Match" : "No match", () => string.Empty);

    internal UnitResult<DesktopError> Set(Maybe<bool> value)
    {
        if (SetProperty(ref accepted, value, nameof(Accepted)))
        {
            OnPropertyChanged(nameof(HasDecision));
            OnPropertyChanged(nameof(IsMatch));
            OnPropertyChanged(nameof(IsNoMatch));
            OnPropertyChanged(nameof(Label));
        }
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Clear() => Set(Maybe<bool>.None);
}
