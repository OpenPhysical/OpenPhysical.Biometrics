// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>The current person and their required biometric enrollment sections.</summary>
public sealed class EnrollmentViewModel : ObservableObject
{
    private readonly FingerprintEnrollmentViewModel fingerprints;

    internal EnrollmentViewModel(
        Func<Maybe<CapturedFingerprint>> source,
        Maybe<CaptureViewModel> live,
        EnrollmentFingerSuggestions suggestions,
        ITemplateSessionStorage storage,
        Func<Result<TemplateMetadata, DesktopError>> metadata,
        Maybe<Func<TemplateSaveOptions>> saveOptions = default
    )
    {
        Person = new PersonEnrollmentViewModel();
        fingerprints = new FingerprintEnrollmentViewModel(source, live, suggestions, Person);
        Sections = [new FingerprintEnrollmentSection(fingerprints, storage, metadata, saveOptions)];
        Person.PropertyChanged += (_, _) => Refresh();
        Fingerprints.PropertyChanged += (_, _) => Refresh();
    }

    /// <summary>Gets the shared editable person identity.</summary>
    public PersonEnrollmentViewModel Person { get; }

    /// <summary>Gets the editable finger plan and capture actions.</summary>
    public IFingerprintEnrollment Fingerprints => fingerprints;

    /// <summary>Gets the registered capture modules for this person.</summary>
    public ImmutableArray<IEnrollmentSection> Sections { get; }

    /// <summary>Gets the command accepting the current capture.</summary>
    public IAsyncRelayCommand AcceptCommand => Fingerprints.AcceptCommand;

    /// <summary>Gets enrollment progress.</summary>
    public string Progress => Fingerprints.Progress;

    /// <summary>Gets the next required operator action.</summary>
    public string Instructions => Fingerprints.Instructions;

    /// <summary>Gets whether identity and every required capture are complete.</summary>
    public bool Ready => Person.Create().IsSuccess && Sections.All(section => section.Complete);

    internal async Task<Result<ImmutableArray<EnrollmentArtifact>, DesktopError>> WriteAsync(
        string folder,
        CancellationToken cancellationToken
    )
    {
        var person = Person.Create();
        if (person.IsFailure)
            return person.Error;
        if (!Ready)
            return new DesktopError(
                "Session.Incomplete",
                "Accept every required capture before saving."
            );
        var artifacts = ImmutableArray.CreateBuilder<EnrollmentArtifact>();
        foreach (var section in Sections)
        {
            var written = await section.WriteAsync(person.Value, folder, cancellationToken);
            if (written.IsFailure)
                return written.Error;
            artifacts.AddRange(written.Value);
        }
        return artifacts.ToImmutable();
    }

    internal async Task<UnitResult<DesktopError>> ResetAsync()
    {
        Person.Reset();
        foreach (var section in Sections)
        {
            var reset = await section.ResetAsync();
            if (reset.IsFailure)
                return reset;
        }
        return Refresh();
    }

    internal UnitResult<DesktopError> BeginStop() => fingerprints.BeginStop();

    internal UnitResult<DesktopError> ReleaseCaptures() => fingerprints.ReleaseCaptures();

    private UnitResult<DesktopError> Refresh()
    {
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(Instructions));
        OnPropertyChanged(nameof(Ready));
        return UnitResult.Success<DesktopError>();
    }
}
