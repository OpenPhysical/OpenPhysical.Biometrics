// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class StudioVerificationCardCommands
{
    private readonly StudioVerificationSession session;
    private readonly StudioVerificationCardViewModel model;
    private readonly StudioVerificationMaterial material;

    internal StudioVerificationCardCommands(
        StudioVerificationSession session,
        StudioVerificationCardViewModel model,
        StudioVerificationMaterial material
    )
    {
        this.session = session;
        this.model = model;
        this.material = material;
        PrimaryCommand = new AsyncRelayCommand(
            VerifyAsync,
            () => session.Enabled && !session.Busy && model.IsReady
        );
        CancelCommand = new RelayCommand(() => model.Cancel(), () => session.Busy);
        ContinueFingerCommand = new RelayCommand(
            () => model.SelectFinger(),
            () => model.AwaitingFinger
        );
        ImportIssuerCommand = new AsyncRelayCommand(
            token => ImportAsync(() => material.ImportCertificateAsync(false, token)),
            () => !session.Busy
        );
        ImportSignerCommand = new AsyncRelayCommand(
            token => ImportAsync(() => material.ImportCertificateAsync(true, token)),
            () => !session.Busy
        );
        ImportPrivacyKeyCommand = new AsyncRelayCommand(
            token => ImportAsync(() => material.ImportPrivacyKeyAsync(token)),
            () => !session.Busy
        );
    }

    internal IAsyncRelayCommand PrimaryCommand { get; }
    internal IRelayCommand ContinueFingerCommand { get; }
    internal IRelayCommand CancelCommand { get; }
    internal IAsyncRelayCommand ImportIssuerCommand { get; }
    internal IAsyncRelayCommand ImportSignerCommand { get; }
    internal IAsyncRelayCommand ImportPrivacyKeyCommand { get; }

    private async Task VerifyAsync(CancellationToken token)
    {
        if (!model.IsReady || !session.Enabled || session.Busy)
            return;
        var selected = model.Input.Select(material);
        var encoded = StudioVerificationPresentation.Pin(model.Input.Pin, model.Input.NeedsPin);
        model.Input.Pin = string.Empty;
        if (selected.IsFailure || encoded.IsFailure)
        {
            if (encoded.IsSuccess)
                Array.Clear(encoded.Value);
            model.Status = selected.IsFailure ? selected.Error.Message : encoded.Error.Message;
            return;
        }
        var key = material.TakePrivacyKey();
        model.ClearResult();
        model.Status = "Checking the card. Keep it in the reader.";
        try
        {
            var outcome = await session.RunAsync(
                (backend, ct) =>
                    backend.VerifyAsync(selected.Value, encoded.Value, key, model.ChooseAsync, ct),
                token
            );
            if (outcome.IsSuccess)
            {
                model.Result = outcome.Value;
                model.Status = StudioVerificationPresentation.Summary(outcome.Value);
            }
            else
                model.Status = outcome.Error.Message;
        }
        finally
        {
            Array.Clear(encoded.Value);
            Array.Clear(key);
            model.Finish();
        }
    }

    private async Task ImportAsync(Func<Task<UnitResult<DesktopError>>> action)
    {
        var imported = await action();
        model.Status = imported.IsFailure
            ? imported.Error.Message
            : "Reference added for the next check.";
        model.Notify();
    }

    internal UnitResult<DesktopError> Notify()
    {
        foreach (
            var command in new[]
            {
                PrimaryCommand,
                ImportIssuerCommand,
                ImportSignerCommand,
                ImportPrivacyKeyCommand,
            }
        )
            command.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        return UnitResult.Success<DesktopError>();
    }
}
