// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Owns a transient package passphrase independently of the current person.</summary>
public sealed class PackageProtectionViewModel : ObservableObject, IDisposable
{
    private readonly PackageSessionSecret owned = new();
    private Maybe<Func<Task<UnitResult<DesktopError>>>> pause;
    private Maybe<Func<bool, Task<bool>>> prompt;
    private string passphrase = string.Empty;
    private string confirmation = string.Empty;
    private string status = string.Empty;
    private bool confirmRequired = true;

    /// <summary>Creates a locked package-protection session.</summary>
    public PackageProtectionViewModel()
    {
        UnlockCommand = new RelayCommand(() => Unlock());
        LockCommand = new AsyncRelayCommand(LockAndPauseAsync);
    }

    /// <summary>Gets or sets the transient input, cleared immediately after submission.</summary>
    public string Passphrase
    {
        get => passphrase;
        set => SetProperty(ref passphrase, value);
    }

    /// <summary>Gets or sets the transient new-passphrase confirmation.</summary>
    public string Confirmation
    {
        get => confirmation;
        set => SetProperty(ref confirmation, value);
    }

    /// <summary>Gets whether a newly established passphrase needs confirmation.</summary>
    public bool ConfirmRequired
    {
        get => confirmRequired;
        private set => SetProperty(ref confirmRequired, value);
    }

    /// <summary>Gets whether an owned passphrase is available in memory.</summary>
    public bool IsUnlocked => owned.Current.HasValue;

    /// <summary>Gets a non-sensitive protection status.</summary>
    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    /// <summary>Gets the command accepting passphrase input into owned memory.</summary>
    public IRelayCommand UnlockCommand { get; }

    /// <summary>Gets the command forgetting the passphrase without deleting saved packages.</summary>
    public IAsyncRelayCommand LockCommand { get; }

    internal UnitResult<DesktopError> AttachPrompt(Func<bool, Task<bool>> request)
    {
        prompt = request;
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Validates and owns the transient passphrase without persisting it.</summary>
    /// <returns>The validated protection state or a typed input failure.</returns>
    public UnitResult<DesktopError> Unlock()
    {
        var expected = owned.Begin();
        if (expected.IsFailure)
        {
            ClearInputs();
            return expected.Error;
        }
        if (ConfirmRequired && Passphrase != Confirmation)
        {
            ClearInputs();
            Status = "The passphrases do not match. Try again.";
            return new DesktopError("Package.Confirmation", Status);
        }
        var encoded = DesktopEdge.Try(
            () => new UTF8Encoding(false, true).GetBytes(Passphrase),
            "Package.PassphraseEncoding",
            "Enter a valid Unicode passphrase."
        );
        if (encoded.IsFailure)
        {
            ClearInputs();
            Status = encoded.Error.Message;
            return encoded.Error;
        }
        return AcceptBytes(encoded.Value, expected.Value);
    }

    private UnitResult<DesktopError> AcceptBytes(byte[] bytes, long expected)
    {
        try
        {
            var accepted = owned.Replace(bytes, expected);
            if (accepted.IsFailure)
            {
                Status = accepted.Error.Message;
                return accepted;
            }
            Status = "Packages unlocked for this session.";
            OnPropertyChanged(nameof(IsUnlocked));
            return UnitResult.Success<DesktopError>();
        }
        finally
        {
            Array.Clear(bytes);
            ClearInputs();
        }
    }

    /// <summary>Forgets the owned passphrase on explicit lock, OS lock or shutdown.</summary>
    /// <returns>Successful secret release.</returns>
    public UnitResult<DesktopError> Lock()
    {
        var expected = owned.Lock();
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            PresentLocked(expected);
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PresentLocked(expected));
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Gets the generation invalidated by a lock or passphrase change.</summary>
    public long Revision => owned.Generation;

    internal async Task<Result<PackageSecret, DesktopError>> RequireAsync(bool establish)
    {
        var available = owned.Begin();
        if (available.IsFailure)
            return available.Error;
        var current = owned.Current;
        if (current.HasValue)
            return current.Value;
        ConfirmRequired = establish;
        Status = string.Empty;
        if (prompt.HasValue && await prompt.Value(establish))
        {
            current = owned.Current;
            if (current.HasValue)
                return current.Value;
        }
        return new DesktopError("Package.Locked", "Unlock package protection to continue.");
    }

    /// <summary>Attaches application cancellation and sensitive-state release to explicit locking.</summary>
    /// <param name="action">Application-owned drain callback.</param>
    /// <returns>Successful attachment.</returns>
    public UnitResult<DesktopError> AttachLockAction(Func<Task<UnitResult<DesktopError>>> action)
    {
        pause = action;
        return UnitResult.Success<DesktopError>();
    }

    private async Task LockAndPauseAsync()
    {
        Lock();
        if (pause.HasValue)
            await pause.Value();
    }

    private UnitResult<DesktopError> PresentLocked(long expected)
    {
        if (owned.Generation != expected)
            return UnitResult.Success<DesktopError>();
        ClearInputs();
        Status = string.Empty;
        OnPropertyChanged(nameof(IsUnlocked));
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> ClearInputs()
    {
        Passphrase = Confirmation = string.Empty;
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Releases the in-memory passphrase.</summary>
    public void Dispose()
    {
        owned.Dispose();
        Lock();
    }
}
