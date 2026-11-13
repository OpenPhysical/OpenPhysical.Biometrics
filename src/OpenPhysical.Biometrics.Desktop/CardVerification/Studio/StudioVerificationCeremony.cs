// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Configuration;
using OpenPhysical.Workstation.Application.Operators;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationCeremony
{
    internal static async Task<Result<CredentialVerificationResult, DesktopError>> RunAsync(
        IServiceProvider services,
        StudioVerificationSelection selection,
        ReadOnlyMemory<byte> pin,
        ReadOnlyMemory<byte> key,
        Func<
            ImmutableArray<GuidedFingerChoice>,
            CancellationToken,
            Task<Result<GuidedFingerChoice, DesktopError>>
        > choose,
        CancellationToken token
    )
    {
        var authorized = await AuthorizeAsync(services, selection, token);
        if (authorized.IsFailure)
            return authorized.Error;
        return selection.Choice.NeedsScanner
            ? await GuidedAsync(services, authorized.Value, selection, pin, key, choose, token)
            : await CardAsync(services, selection, pin, key, token);
    }

    private static async Task<Result<VerifiedBundleSnapshot, DesktopError>> AuthorizeAsync(
        IServiceProvider services,
        StudioVerificationSelection selection,
        CancellationToken token
    )
    {
        if (services.GetRequiredService<WorkstationClockConfidence>().Uncertainty.HasNoValue)
            return new DesktopError(
                "Verification.ClockSetup",
                "Your administrator needs to configure trusted time and restart Studio before checking cards."
            );
        var actor = await services.GetRequiredService<OperatorSessionService>().CurrentAsync(token);
        if (actor.IsFailure || actor.Value.HasNoValue)
            return new DesktopError("Verification.SignIn", "Sign in before checking a card.");
        var configuration = await services
            .GetRequiredService<ConfigurationActivationService>()
            .ReadCurrentAsync(token);
        if (configuration.IsFailure)
            return StudioVerificationErrors.From(configuration.Error);
        if (!StudioVerificationChoices.Read(configuration.Value).Contains(selection.Choice))
            return new DesktopError(
                "Verification.Policy",
                "This card check is not available in the current signed settings."
            );
        return configuration.Value;
    }

    private static async Task<Result<CredentialVerificationResult, DesktopError>> CardAsync(
        IServiceProvider services,
        StudioVerificationSelection selection,
        ReadOnlyMemory<byte> pin,
        ReadOnlyMemory<byte> key,
        CancellationToken token
    )
    {
        if (!key.IsEmpty)
            return new DesktopError(
                "Verification.Input",
                "A privacy key is only used for TWIC checks."
            );
        var request = new LocalVerificationRequest(
            selection.ReaderId,
            selection.InterfaceType,
            selection.Choice.Method,
            selection.IssuerCertificates
        )
        {
            ProfileId = selection.Choice.ProfileId,
        };
        var result = await services
            .GetRequiredService<LocalVerificationService>()
            .VerifyCardAsync(request, pin, token);
        return result.IsSuccess ? result.Value : StudioVerificationErrors.From(result.Error);
    }

    private static async Task<Result<CredentialVerificationResult, DesktopError>> GuidedAsync(
        IServiceProvider services,
        VerifiedBundleSnapshot bundle,
        StudioVerificationSelection selection,
        ReadOnlyMemory<byte> pin,
        ReadOnlyMemory<byte> key,
        Func<
            ImmutableArray<GuidedFingerChoice>,
            CancellationToken,
            Task<Result<GuidedFingerChoice, DesktopError>>
        > choose,
        CancellationToken token
    )
    {
        await using var operation = new GuidedVerificationCoordinator(
            services.GetRequiredService<IGuidedVerificationRunner>(),
            services.GetRequiredService<TimeProvider>()
        );
        var request = new GuidedVerificationRequest(
            selection.ReaderId,
            selection.InterfaceType,
            selection.ScannerId,
            selection.Choice.Kind == StudioVerificationKind.TwicFingerprint
                ? GuidedCredentialKind.Twic
                : GuidedCredentialKind.Piv,
            selection.Choice.ProfileId,
            selection.MarsecLevel,
            selection.IssuerCertificates,
            selection.ContentSignerCertificates
        );
        var started = operation.Start(request, bundle, pin.Span, key.Span);
        if (started.IsFailure)
            return StudioVerificationErrors.From(started.Error);
        return await StudioGuidedVerification.WaitAsync(operation, started.Value, choose, token);
    }
}
