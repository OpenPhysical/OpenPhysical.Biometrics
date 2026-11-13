// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Cli.Commands;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Desktop;
using OpenPhysical.Cli.Hosting;
using OpenPhysical.Middleware.Cli.Inspection;
using OpenPhysical.Middleware.Cli.Settings;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Trust;
using Org.BouncyCastle.X509;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Runs guided local biometric verification through the shared card and capture services.</summary>
/// <param name="inputs">Inputs.</param>
/// <param name="cards">Cards.</param>
/// <param name="fingerprints">Fingerprints.</param>
/// <param name="renderer">Renderer.</param>
/// <param name="output">Bounded command output destination.</param>
/// <param name="console">Selected interactive console.</param>
/// <param name="cancellationToken">Cancellation for this operation.</param>
public sealed class FingerprintVerifyCommand(
    OfflineVerificationInputReader inputs,
    CliCardSessionSource cards,
    LiveFingerprintVerificationService fingerprints,
    OperationResultRenderer renderer,
    CommandOutputOptions output,
    IAnsiConsole console,
    CancellationToken cancellationToken = default
) : AsyncCommand<FingerprintVerifySettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        FingerprintVerifySettings settings
    )
    {
        if (
            (output.NonInteractive || Console.IsInputRedirected)
            && (!settings.CaptureReady || !settings.PinStdin)
        )
            return renderer.RenderFailure(new(OperationFailureKind.Validation, Guid.NewGuid()));
        var input = await ReadRequestAsync(settings).ConfigureAwait(false);
        if (input.IsFailure)
            return CredentialVerificationConsole.RenderInputFailure(renderer, input.Error);
        var signers = await OfflineVerificationInputReader
            .ReadContentSignersAsync(settings.ContentSignerPaths, cancellationToken)
            .ConfigureAwait(false);
        return signers.IsFailure
            ? CredentialVerificationConsole.RenderInputFailure(renderer, signers.Error)
            : await VerifyWithCardAsync(settings, input.Value, signers.Value).ConfigureAwait(false);
    }

    private Task<
        Result<CredentialVerificationRequest, LocalVerificationCacheFailure>
    > ReadRequestAsync(FingerprintVerifySettings settings) =>
        inputs.ReadRequestAsync(
            settings.AuthorityPath,
            settings.DatabasePath,
            settings.SnapshotPath,
            CredentialEvidenceCodec.CardAuthenticationProfile,
            CredentialVerificationWorkflow.LocalVerification,
            cancellationToken
        );

    private Result<ILocalCardSession, VerificationError> OpenCard(
        FingerprintVerifySettings settings
    ) =>
        cards.Open(
            settings.ReaderProvider,
            settings.Reader,
            settings.Interface == "contactless"
                ? PivInterfaceType.Contactless
                : PivInterfaceType.Contact
        );

    private async Task<int> VerifyWithCardAsync(
        FingerprintVerifySettings settings,
        CredentialVerificationRequest request,
        ImmutableArray<X509Certificate> signers
    )
    {
        var opened = OpenCard(settings);
        if (opened.IsFailure)
            return renderer.RenderFailure(new(OperationFailureKind.Unavailable, Guid.NewGuid()));
        using var lease = opened.Value;
        byte[] pin = [];
        try
        {
            var read = await DesktopEdge
                .TryValidatedAsync(
                    () =>
                        CliPinInput.ReadAsync(
                            settings.PinStdin,
                            !output.NonInteractive,
                            console,
                            cancellationToken
                        ),
                    "Pin.Invalid",
                    "Pin.Unavailable",
                    "The PIN could not be read."
                )
                .ConfigureAwait(false);
            if (read.IsFailure)
                return RenderPinFailure(read.Error);
            pin = read.Value;
            return await VerifyAndRenderAsync(lease, request, signers, settings, pin)
                .ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(pin);
        }
    }

    private async Task<int> VerifyAndRenderAsync(
        ILocalCardSession lease,
        CredentialVerificationRequest request,
        ImmutableArray<X509Certificate> signers,
        FingerprintVerifySettings settings,
        ReadOnlyMemory<byte> pin
    )
    {
        var result = await CaptureVerificationEdge
            .RunAsync(
                () => VerifyAsync(lease, request, signers, settings, pin),
                "capture.verification-unavailable"
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
        CredentialVerificationRequest request,
        ImmutableArray<X509Certificate> signers,
        FingerprintVerifySettings settings,
        ReadOnlyMemory<byte> pin
    ) =>
        fingerprints.VerifyAsync(
            lease,
            request,
            signers,
            settings.CaptureDevice,
            pin,
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

    private int RenderPinFailure(DesktopError error) =>
        renderer.RenderFailure(
            new(
                error.Code switch
                {
                    "Pin.Invalid" => OperationFailureKind.Validation,
                    "Cancelled" => OperationFailureKind.Cancelled,
                    _ => OperationFailureKind.Unavailable,
                },
                Guid.NewGuid()
            )
        );
}
