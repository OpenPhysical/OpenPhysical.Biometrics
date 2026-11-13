// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Cli.Commands;

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Cli.Hosting;
using OpenPhysical.Middleware.Cli.Inspection;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Trust;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Runs the shared guided TWIC workflow using an authenticated local policy bundle.</summary>
/// <param name="inputs">Inputs.</param>
/// <param name="cards">Cards.</param>
/// <param name="twic">Twic.</param>
/// <param name="renderer">Renderer.</param>
/// <param name="output">Bounded command output destination.</param>
/// <param name="console">Selected interactive console.</param>
/// <param name="cancellationToken">Cancellation for this operation.</param>
public sealed class TwicVerifyCommand(
    OfflineVerificationInputReader inputs,
    CliCardSessionSource cards,
    LiveTwicFingerprintVerificationService twic,
    OperationResultRenderer renderer,
    CommandOutputOptions output,
    IAnsiConsole console,
    CancellationToken cancellationToken = default
) : AsyncCommand<TwicVerifySettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        TwicVerifySettings settings
    )
    {
        if ((output.NonInteractive || Console.IsInputRedirected) && !settings.CaptureReady)
            return renderer.RenderFailure(new(OperationFailureKind.Validation, Guid.NewGuid()));
        var input = await ReadRequestAsync(settings).ConfigureAwait(false);
        if (input.IsFailure)
            return CredentialVerificationConsole.RenderInputFailure(renderer, input.Error);
        var profile = TwicAcceptanceService.ReadProfile(input.Value.Bundle, settings.Profile);
        return profile.IsFailure
            ? renderer.RenderFailure(new(OperationFailureKind.Unavailable, Guid.NewGuid()))
            : await VerifyWithPrivacyKeyAsync(settings, input.Value, profile.Value)
                .ConfigureAwait(false);
    }

    private Task<
        Result<CredentialVerificationRequest, LocalVerificationCacheFailure>
    > ReadRequestAsync(TwicVerifySettings settings) =>
        inputs.ReadRequestAsync(
            settings.AuthorityPath,
            settings.DatabasePath,
            settings.SnapshotPath,
            settings.Profile,
            CredentialVerificationWorkflow.Twic,
            cancellationToken
        );

    private async Task<int> VerifyWithPrivacyKeyAsync(
        TwicVerifySettings settings,
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile
    )
    {
        Maybe<TwicPrivacyKeyContainer> key = Maybe<TwicPrivacyKeyContainer>.None;
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.PrivacyKeyPath))
            {
                var loaded = await OfflineVerificationInputReader
                    .ReadTwicPrivacyKeyAsync(settings.PrivacyKeyPath, cancellationToken)
                    .ConfigureAwait(false);
                if (loaded.IsFailure)
                    return CredentialVerificationConsole.RenderInputFailure(renderer, loaded.Error);
                key = loaded.Value;
            }
            return await VerifyWithCardAsync(settings, request, profile, key).ConfigureAwait(false);
        }
        finally
        {
            if (key.HasValue && ImmutableCollectionsMarshal.AsArray(key.Value.Key) is byte[] owned)
                Array.Clear(owned);
        }
    }

    private Result<ILocalCardSession, VerificationError> OpenCard(TwicVerifySettings settings) =>
        cards.Open(
            settings.ReaderProvider,
            settings.Reader,
            settings.Interface == "contactless"
                ? PivInterfaceType.Contactless
                : PivInterfaceType.Contact
        );

    private async Task<int> VerifyWithCardAsync(
        TwicVerifySettings settings,
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        Maybe<TwicPrivacyKeyContainer> key
    )
    {
        var opened = OpenCard(settings);
        if (opened.IsFailure)
            return renderer.RenderFailure(new(OperationFailureKind.Unavailable, Guid.NewGuid()));
        using var lease = opened.Value;
        var result = await CaptureVerificationEdge
            .RunAsync(
                () => VerifyAsync(lease, settings, request, profile, key),
                "twic.verification-unavailable"
            )
            .ConfigureAwait(false);
        if (result.IsFailure)
            return RenderCaptureFailure(result.Error);
        lease.Dispose();
        return CredentialVerificationConsole.RenderResult(
            result.Value,
            request.ProfileId,
            request.Workflow,
            renderer,
            output,
            console
        );
    }

    private Task<Result<CredentialVerificationResult, VerificationError>> VerifyAsync(
        ILocalCardSession lease,
        TwicVerifySettings settings,
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        Maybe<TwicPrivacyKeyContainer> key
    ) =>
        twic.VerifyAsync(
            lease,
            request,
            profile,
            settings.MarsecLevel!.Value,
            key,
            settings.CaptureDevice,
            (choices, token) =>
                CliFingerprintGuidance.SelectAsync(choices, settings, output, console, token),
            cancellationToken
        );

    private int RenderCaptureFailure(VerificationError error) =>
        renderer.RenderFailure(
            new(
                error.Code switch
                {
                    VerificationErrorCodes.Cancelled => OperationFailureKind.Cancelled,
                    VerificationErrorCodes.HolderMismatch => OperationFailureKind.Denied,
                    _ => OperationFailureKind.Unavailable,
                },
                Guid.NewGuid()
            )
        );
}
