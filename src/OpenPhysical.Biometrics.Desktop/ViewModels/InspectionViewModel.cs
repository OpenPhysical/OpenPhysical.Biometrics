// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Operator workflow for loading, comparing and clearing fingerprint references.</summary>
public sealed class InspectionViewModel : InspectionDisplayViewModel
{
    private readonly ITemplateStorage storage;
    private readonly DesktopFilePicker picker;
    private readonly ICapturedFingerprintSource capture;
    private readonly TemplateComparison comparison;
    private Maybe<TemplateDocument> document;
    private bool stopping;
    private int generation;

    /// <summary>Creates the file, credential and capture comparison workflow.</summary>
    /// <param name="storage">Reference document storage.</param>
    /// <param name="picker">Native file picker.</param>
    /// <param name="capture">Observed live and frozen capture source.</param>
    /// <param name="comparison">Reusable canonical comparison service.</param>
    /// <param name="credentialReader">Inserted-credential reader service.</param>
    public InspectionViewModel(
        ITemplateStorage storage,
        DesktopFilePicker picker,
        ICapturedFingerprintSource capture,
        TemplateComparison comparison,
        CredentialReaderService credentialReader
    )
        : base(capture)
    {
        this.storage = storage;
        this.picker = picker;
        this.capture = capture;
        this.comparison = comparison;
        Reference = new ReferenceSelectionViewModel(selected =>
        {
            generation++;
            ResetDecision();
            RefreshProbe();
            return ShowReference(document, selected.Index);
        });
        BrowseCommand = new AsyncRelayCommand(BrowseAsync, () => !stopping);
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !stopping);
        CompareCommand = new AsyncRelayCommand(CompareAsync, () => !stopping);
        Credential = new CredentialViewModel(credentialReader, SetDocument, ClearReference);
    }

    /// <summary>Gets the actual reference views and selected view.</summary>
    public ReferenceSelectionViewModel Reference { get; }

    /// <summary>Gets the reference-file browse command.</summary>
    public IAsyncRelayCommand BrowseCommand { get; }

    /// <summary>Gets the reference-file load command.</summary>
    public IAsyncRelayCommand LoadCommand { get; }

    /// <summary>Gets the current-input comparison command.</summary>
    public IAsyncRelayCommand CompareCommand { get; }

    /// <summary>Gets the inserted-credential reader workflow.</summary>
    public CredentialViewModel Credential { get; }

    /// <summary>Stops new operations, drains active work and releases owned scenes.</summary>
    /// <returns>Shutdown success or a typed cleanup failure.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopping = true;
        BeginStopDisplay();
        generation++;
        BrowseCommand.NotifyCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
        CompareCommand.NotifyCanExecuteChanged();
        LoadCommand.Cancel();
        var credential = Credential.StopAsync();
        var drained = await DesktopCommandTasks.DrainAsync(
            BrowseCommand,
            LoadCommand,
            CompareCommand
        );
        StopDisplay();
        document = Maybe<TemplateDocument>.None;
        comparison.Clear();
        var credentialStopped = await credential;
        if (credentialStopped.IsFailure)
            return credentialStopped;
        return drained;
    }

    private async Task BrowseAsync()
    {
        var selected = await picker.OpenAsync();
        if (stopping)
            return;
        if (selected.IsFailure)
        {
            if (selected.Error.Code != "Cancelled")
                Report = selected.Error.Message;
            return;
        }
        FilePath = selected.Value;
        await DesktopCommandTasks.DrainAsync(LoadCommand);
        if (!stopping)
            await LoadCommand.ExecuteAsync(default);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Credential.Invalidate();
        var expected = ++generation;
        var loaded = await storage.LoadAsync(FilePath, cancellationToken);
        if (stopping || expected != generation)
            return;
        if (loaded.IsFailure)
        {
            Report = $"{loaded.Error.Message} ({loaded.Error.Code})";
            return;
        }
        SetDocument(loaded.Value);
    }

    private UnitResult<DesktopError> SetDocument(TemplateDocument value)
    {
        if (stopping)
            return UnitResult.Success<DesktopError>();
        generation++;
        document = value;
        ResetDecision();
        RefreshProbe();
        Summary = "Reference ready. Select the corresponding finger view.";
        comparison.Clear();
        Reference.Replace(value.Record);
        return ShowReference(document, Reference.SelectedView.Index);
    }

    /// <summary>Clears the current reference, PIN, probe and decision for a new person.</summary>
    /// <param name="nextFinger">The fresh enrollment plan primary finger.</param>
    /// <returns>Complete clearing or a typed cleanup failure.</returns>
    public async Task<UnitResult<DesktopError>> ClearPersonAsync(FingerChoice nextFinger)
    {
        LoadCommand.Cancel();
        var credential = Credential.ClearForPerson();
        AssignedProbeFinger = nextFinger;
        ClearReference();
        Reference.Forget();
        FilePath = string.Empty;
        ClearPersonProbe();
        var drained = await DesktopCommandTasks.DrainAsync(
            LoadCommand,
            CompareCommand,
            Credential.ReadCommand,
            Credential.RefreshCommand
        );
        return credential.IsFailure ? credential : drained;
    }

    internal UnitResult<DesktopError> ClearReference()
    {
        if (stopping)
            return UnitResult.Success<DesktopError>();
        generation++;
        document = Maybe<TemplateDocument>.None;
        ResetDecision();
        Scene = SceneLifetime.Clear(Scene);
        RefreshProbe();
        Reference.Clear();
        comparison.Clear();
        Summary = string.Empty;
        Report = "Open a reference to inspect its finger views.";
        return UnitResult.Success<DesktopError>();
    }

    private Task CompareAsync() =>
        InspectionComparisonRunner.RunAsync(
            comparison,
            () =>
                InspectionComparisonRequest.Create(
                    document,
                    capture.Captured,
                    AssignedProbeFinger.Position,
                    Reference.SelectedView.Index,
                    Threshold,
                    generation,
                    DecisionRevision
                ),
            request =>
                !stopping
                && request.IsCurrent(
                    document,
                    capture.Captured,
                    AssignedProbeFinger.Position,
                    Reference.SelectedView.Index,
                    Threshold,
                    generation,
                    DecisionRevision
                ),
            PublishComparison,
            FailedComparison
        );
}
