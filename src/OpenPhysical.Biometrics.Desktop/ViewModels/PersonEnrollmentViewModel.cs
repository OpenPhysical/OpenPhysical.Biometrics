// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Editable identity shared by every enrollment section for the current person.</summary>
public sealed class PersonEnrollmentViewModel : ObservableObject
{
    private string name = string.Empty;
    private string identifier = string.Empty;

    /// <summary>Gets or sets the operator-provided name or alias.</summary>
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    /// <summary>Gets or sets an optional local subject identifier.</summary>
    public string Identifier
    {
        get => identifier;
        set => SetProperty(ref identifier, value);
    }

    internal Result<CapturePerson, DesktopError> Create() => CapturePerson.Create(Name, Identifier);

    internal UnitResult<DesktopError> Reset()
    {
        Name = string.Empty;
        Identifier = string.Empty;
        return UnitResult.Success<DesktopError>();
    }
}
