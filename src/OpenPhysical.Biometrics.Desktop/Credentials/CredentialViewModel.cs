// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

/// <summary>Operator workflow for reading a credential reference and monitoring removal.</summary>
public sealed class CredentialViewModel : ObservableObject
{
    private readonly CredentialReaderService reader;
    private readonly Func<TemplateDocument, UnitResult<DesktopError>> loaded;
    private readonly Func<UnitResult<DesktopError>> clear;
    private ImmutableArray<string> names = [];
    private string selected = string.Empty;
    private string pin = string.Empty;
    private string status = "Choose a reader and enter the card PIN.";
    private readonly CredentialMonitor monitor = new();
    private bool stopping;
    private int generation;

    /// <summary>Creates the credential operator workflow.</summary>
    /// <param name="reader">Owned reader operations.</param>
    /// <param name="loaded">Publishes a successfully read reference document.</param>
    /// <param name="clear">Invalidates the displayed reference.</param>
    public CredentialViewModel(
        CredentialReaderService reader,
        Func<TemplateDocument, UnitResult<DesktopError>> loaded,
        Func<UnitResult<DesktopError>> clear
    )
    {
        this.reader = reader;
        this.loaded = loaded;
        this.clear = clear;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !stopping);
        ReadCommand = new AsyncRelayCommand(
            ReadAsync,
            () =>
                !stopping
                && !string.IsNullOrWhiteSpace(SelectedReader)
                && Pin.Length is >= 6 and <= 8
        );
    }

    /// <summary>Gets the currently available reader names.</summary>
    public ImmutableArray<string> ReaderNames
    {
        get => names;
        private set => SetProperty(ref names, value);
    }

    /// <summary>Gets or sets the reader, invalidating a reference from the previous reader.</summary>
    public string SelectedReader
    {
        get => selected;
        set
        {
            if (value is not null && SetProperty(ref selected, value))
            {
                Invalidate();
                ReadCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets or sets the temporary PIN, cleared when reading starts.</summary>
    public string Pin
    {
        get => pin;
        set
        {
            if (SetProperty(ref pin, value))
                ReadCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Gets the reader operation status.</summary>
    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    /// <summary>Gets the connected-reader refresh command.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Gets the inserted-credential read command.</summary>
    public IAsyncRelayCommand ReadCommand { get; }

    /// <summary>Stops reads and removal monitoring and drains active commands.</summary>
    /// <returns>Shutdown success or a typed cleanup failure.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopping = true;
        ReadCommand.Cancel();
        RefreshCommand.Cancel();
        RefreshCommand.NotifyCanExecuteChanged();
        ReadCommand.NotifyCanExecuteChanged();
        Invalidate();
        return await DesktopCommandTasks.DrainAsync(ReadCommand, RefreshCommand);
    }

    internal UnitResult<DesktopError> Invalidate()
    {
        generation++;
        ReadCommand.Cancel();
        Pin = string.Empty;
        var stopped = monitor.Stop();
        clear();
        return stopped;
    }

    internal UnitResult<DesktopError> ClearForPerson()
    {
        var cleared = Invalidate();
        Status = "Insert a credential and enter its PIN to load a reference.";
        return cleared;
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        var result = await reader.ReadersAsync(token);
        if (stopping)
            return;
        if (result.IsFailure)
        {
            Status = result.Error.Message;
            return;
        }
        ReaderNames = result.Value;
        if (!ReaderNames.Contains(SelectedReader))
            SelectedReader = ReaderNames.FirstOrDefault() ?? string.Empty;
        Status = ReaderNames.IsEmpty
            ? "Connect a card reader, then refresh."
            : "Insert the credential and enter its PIN.";
    }

    private async Task ReadAsync(CancellationToken token)
    {
        if (stopping)
            return;
        var supplied = Pin.ToCharArray();
        Pin = string.Empty;
        var expected = ++generation;
        clear();
        try
        {
            var watching = monitor.Start(reader, SelectedReader, () => Removed(expected));
            if (watching.IsFailure)
            {
                Status = watching.Error.Message;
                return;
            }
            Status = "Reading the inserted credential…";
            await ReadCardAsync(supplied, expected, token);
        }
        finally
        {
            Array.Clear(supplied);
        }
    }

    private async Task ReadCardAsync(char[] supplied, int expected, CancellationToken token)
    {
        var result = await reader.ReadAsync(SelectedReader, supplied, token);
        if (stopping || expected != generation)
            return;
        if (result.IsFailure)
        {
            monitor.Stop();
            Status = $"{result.Error.Message} ({result.Error.Code})";
            return;
        }
        var accepted = loaded(result.Value);
        Status = accepted.IsSuccess ? "Credential fingerprints loaded." : accepted.Error.Message;
    }

    private UnitResult<DesktopError> Removed(int expected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (stopping || expected != generation)
                return;
            Invalidate();
            Status = "Credential removed. Insert and read a credential to compare.";
        });
        return UnitResult.Success<DesktopError>();
    }
}
