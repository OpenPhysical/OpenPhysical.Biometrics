// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Editable identity shared by every enrollment section for the current person.</summary>
public sealed class PersonEnrollmentViewModel : ObservableObject
{
    private string name = string.Empty;
    private string identifier = string.Empty;
    private Maybe<Guid> authoritativePersonId;

    /// <summary>Gets or sets the operator-provided name or alias.</summary>
    public string Name
    {
        get => name;
        set
        {
            if (SetProperty(ref name, value))
            {
                ForgetAuthority();
                OnPropertyChanged(nameof(NameMissing));
            }
        }
    }

    /// <summary>Gets whether the required person label is empty or only whitespace.</summary>
    public bool NameMissing => string.IsNullOrWhiteSpace(Name);

    /// <summary>Gets or sets an optional local subject identifier.</summary>
    public string Identifier
    {
        get => identifier;
        set
        {
            if (SetProperty(ref identifier, value))
                ForgetAuthority();
        }
    }

    /// <summary>Gets the trusted Directory or server identity selected before acquisition.</summary>
    public Maybe<Guid> AuthoritativePersonId => authoritativePersonId;

    internal UnitResult<DesktopError> Bind(
        Guid personId,
        string displayName,
        string subjectIdentifier
    )
    {
        var person = CapturePerson.Create(displayName, subjectIdentifier);
        if (personId == Guid.Empty || person.IsFailure)
            return new DesktopError(
                "Person.Selection",
                "Select a valid authoritative person before capture."
            );
        Name = displayName;
        Identifier = subjectIdentifier;
        authoritativePersonId = personId;
        OnPropertyChanged(nameof(AuthoritativePersonId));
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> ForgetAuthority()
    {
        authoritativePersonId = Maybe<Guid>.None;
        OnPropertyChanged(nameof(AuthoritativePersonId));
        return UnitResult.Success<DesktopError>();
    }

    internal Result<CapturePerson, DesktopError> Create() => CapturePerson.Create(Name, Identifier);

    internal UnitResult<DesktopError> Reset()
    {
        Name = string.Empty;
        Identifier = string.Empty;
        ForgetAuthority();
        return UnitResult.Success<DesktopError>();
    }
}
