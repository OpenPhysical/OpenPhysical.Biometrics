// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Owns the card-check task's private installation and cancellation lifetime.</summary>
public sealed class StudioCardVerificationViewModel
{
    private readonly StudioVerificationSession session;
    private bool stopped;

    /// <summary>Uses an application-owned verification runtime and native file picker.</summary>
    /// <param name="factory">Creates the owned read-only runtime.</param>
    /// <param name="picker">Native application-owned file picker.</param>
    public StudioCardVerificationViewModel(
        IStudioVerificationBackendFactory factory,
        DesktopFilePicker picker
    )
    {
        session = new StudioVerificationSession(factory);
        Setup = new StudioVerificationSetupViewModel(session, picker);
        Card = new StudioVerificationCardViewModel(session, Setup, picker);
    }

    /// <summary>Pairing, operator sign-in and signed configuration.</summary>
    public StudioVerificationSetupViewModel Setup { get; }

    /// <summary>Card and guided holder verification.</summary>
    public StudioVerificationCardViewModel Card { get; }

    /// <summary>Opens current protected settings when this task becomes active.</summary>
    /// <returns>Activation outcome.</returns>
    public async Task<UnitResult<DesktopError>> ActivateAsync()
    {
        if (stopped)
            return new DesktopError("Verification.Closed", "Card checking has stopped.");
        session.Enable();
        await Setup.RefreshCommand.ExecuteAsync(default);
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Cancels and drains all checks before clearing inputs and releasing the installation.</summary>
    /// <returns>Cancellation, drain and cleanup outcome.</returns>
    public async Task<UnitResult<DesktopError>> PauseAsync()
    {
        session.Disable();
        Setup.Cancel();
        Card.Cancel();
        var drained = await DesktopCommandTasks.DrainAsync(
            Setup.PrimaryCommand,
            Setup.RefreshCommand,
            Setup.SignOutCommand,
            Setup.UnpairCommand,
            Card.PrimaryCommand,
            Card.ImportIssuerCommand,
            Card.ImportSignerCommand,
            Card.ImportPrivacyKeyCommand
        );
        Card.Clear();
        Setup.Clear();
        var closed = await session.CloseAsync();
        return drained.IsFailure ? drained : closed;
    }

    /// <summary>Permanently stops this task before the application host closes.</summary>
    /// <returns>Permanent shutdown outcome.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopped = true;
        return await PauseAsync();
    }
}
