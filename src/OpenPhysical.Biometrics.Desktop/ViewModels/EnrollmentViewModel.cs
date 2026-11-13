// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>One identity, selected capture plan, and accepted artifacts for a person.</summary>
public sealed class EnrollmentViewModel : ObservableObject
{
    private readonly EnrollmentSections catalog;
    private readonly EnrollmentSectionNavigation navigation;
    private readonly EnrollmentStructure structure;
    private CapturePlanChoice plan;

    /// <summary>Creates a workspace from registered modality sections sharing one identity.</summary>
    /// <param name="person">The single shared local identity.</param>
    /// <param name="sections">Registered capture sections in default workflow order.</param>
    private EnrollmentViewModel(
        PersonEnrollmentViewModel person,
        ImmutableArray<IEnrollmentSection> sections
    )
    {
        Person = person;
        catalog = new(sections);
        plan = catalog.DefaultPlan;
        structure = new(sections, sections[0], name => OnPropertyChanged(name));
        structure.Plan(plan);
        navigation = new(sections[0], Refresh);
        AcceptCommand = new AsyncRelayCommand(
            AcceptAsync,
            () => !IsBusy && navigation.Selected.AcceptCommand.CanExecute(default)
        );
        SelectSectionCommand = new AsyncRelayCommand<IEnrollmentSection>(
            section => section is null ? Task.CompletedTask : SelectAsync(section),
            _ => !IsBusy
        );
        Person.PropertyChanged += (_, _) => Refresh();
        foreach (var section in catalog.Registered)
            section.ViewModel.PropertyChanged += (_, _) => Refresh();
    }

    /// <summary>Validates and creates a workspace from nonempty, uniquely identified capture sections.</summary>
    /// <param name="person">Identity shared by all sections.</param>
    /// <param name="sections">Registered sections in default workflow order.</param>
    /// <returns>The validated workspace or a precise registration failure.</returns>
    public static Result<EnrollmentViewModel, DesktopError> Create(
        PersonEnrollmentViewModel person,
        ImmutableArray<IEnrollmentSection> sections
    ) => EnrollmentSections.Validate(sections).Map(() => new EnrollmentViewModel(person, sections));

    /// <summary>Gets the identity shared by all capture modalities.</summary>
    public PersonEnrollmentViewModel Person { get; }

    /// <summary>Gets the registered fingerprint planning controls.</summary>
    public IFingerprintEnrollment Fingerprints =>
        (IFingerprintEnrollment)
            catalog.Registered.Single(section => section.Id == "fingerprints").ViewModel;

    /// <summary>Gets all available capture plans.</summary>
    public ImmutableArray<CapturePlanChoice> Plans => catalog.Plans;

    /// <summary>Gets or changes the required set of biometric objects.</summary>
    public CapturePlanChoice Plan
    {
        get => plan;
        set
        {
            if (
                navigation.Stopping
                || IsBusy
                || IsEditing
                || !Plans.Contains(value)
                || value == plan
            )
                return;
            plan = value;
            structure.Plan(plan);
            Refresh();
            if (!Sections.Contains(navigation.Selected))
                _ = SelectAsync(Sections[0]);
        }
    }

    /// <summary>Gets the capture sections required by this plan.</summary>
    public ImmutableArray<IEnrollmentSection> Sections => structure.Required;

    /// <summary>Gets or selects the modality occupying the shared capture workspace.</summary>
    public IEnrollmentSection ActiveSection
    {
        get => navigation.Selected;
        set
        {
            if (!navigation.Stopping && !IsBusy && Sections.Contains(value))
                _ = SelectAsync(value);
        }
    }

    /// <summary>Gets whether the facial workspace is selected.</summary>
    public bool FaceActive => navigation.Selected.Id == "face";

    /// <summary>Gets whether the fingerprint workspace is selected.</summary>
    public bool FingerprintsActive => navigation.Selected.Id == "fingerprints";

    /// <summary>Gets whether an operation prevents changing the active task.</summary>
    public bool IsBusy => navigation.Changing || catalog.Registered.Any(section => section.IsBusy);

    /// <summary>Gets whether changing capture tasks or plans is safe.</summary>
    public bool CanNavigate => !navigation.Stopping && !IsBusy && !IsEditing;

    /// <summary>Gets whether a modality-specific adjustment draft is open.</summary>
    public bool IsEditing => catalog.Registered.Any(section => section.IsEditing);

    /// <summary>Gets whether every section can safely discard its current attempt.</summary>
    public bool CanReset =>
        !navigation.Changing && catalog.Registered.All(section => section.CanReset);

    /// <summary>Gets accepted-section progress without implying any unsigned object is issued.</summary>
    public string Progress =>
        Sections.Length == 1
            ? navigation.Selected.Progress
            : $"{Sections.Sum(section => section.AcceptedCount)} of {Sections.Sum(section => section.RequiredCount)} captures accepted";

    /// <summary>Gets the next useful action for the active modality.</summary>
    public string Instructions =>
        navigation.Problem.Length > 0 ? navigation.Problem : navigation.Selected.Instructions;

    /// <summary>Gets whether identity and all selected modalities are accepted.</summary>
    public bool Ready =>
        !IsBusy
        && !IsEditing
        && Person.Create().IsSuccess
        && Sections.All(section => section.Complete);

    /// <summary>Gets the active task's explicit accept operation.</summary>
    public IAsyncRelayCommand AcceptCommand { get; }

    /// <summary>Gets the command selecting a registered capture section.</summary>
    public IAsyncRelayCommand<IEnrollmentSection> SelectSectionCommand { get; }

    /// <summary>Gets all modality operations participating in session shutdown.</summary>
    public ImmutableArray<IAsyncRelayCommand> Commands =>
        [AcceptCommand, .. catalog.Registered.SelectMany(section => section.Commands)];

    internal Result<ImmutableArray<PackageArtifact>, DesktopError> PrepareArtifacts() =>
        EnrollmentSectionArtifacts.Prepare(Sections, Ready);

    /// <summary>Activates the chosen source only while the Capture workspace is visible.</summary>
    /// <param name="visible">Whether the Capture workspace is currently visible.</param>
    /// <returns>The selected source's activation outcome.</returns>
    public Task<UnitResult<DesktopError>> ActivateAsync(bool visible) =>
        navigation.ActivateAsync(visible);

    internal async Task<UnitResult<DesktopError>> ResetAsync()
    {
        var reset = await EnrollmentSectionLifecycle.ResetAsync(catalog.Registered);
        Person.Reset();
        plan = catalog.DefaultPlan;
        structure.Plan(plan);
        var activated = await navigation.SelectAsync(Sections[0]);
        Refresh();
        return reset.IsFailure ? reset : activated;
    }

    internal async Task<UnitResult<DesktopError>> StopAsync()
    {
        navigation.Stopping = true;
        AcceptCommand.Cancel();
        SelectSectionCommand.Cancel();
        var stopped = await EnrollmentSectionLifecycle.StopAsync(catalog.Registered);
        var drained = await DesktopCommandTasks.DrainAsync(AcceptCommand, SelectSectionCommand);
        return stopped.IsFailure ? stopped : drained;
    }

    private async Task AcceptAsync()
    {
        if (navigation.Stopping)
            return;
        await navigation.Selected.AcceptCommand.ExecuteAsync(default);
        if (navigation.Selected.Complete)
        {
            var next = Sections
                .Where(section => !section.Complete)
                .Select(Maybe.From)
                .FirstOrDefault();
            if (next.HasValue)
                await SelectAsync(next.Value);
        }
        Refresh();
    }

    private Task SelectAsync(IEnrollmentSection section) =>
        navigation.Stopping
        || IsEditing
        || ReferenceEquals(section, navigation.Selected)
        || !Sections.Contains(section)
            ? Task.CompletedTask
            : navigation.SelectAsync(section);

    private UnitResult<DesktopError> Refresh()
    {
        if (navigation.Stopping)
            return UnitResult.Success<DesktopError>();
        return structure.Refresh(navigation.Selected, AcceptCommand, SelectSectionCommand);
    }
}
