// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Workstation.Application.Configuration;
using OpenPhysical.Workstation.Application.Devices;
using OpenPhysical.Workstation.Application.Operators;
using OpenPhysical.Workstation.Application.Pairing;
using OpenPhysical.Workstation.Application.Verification;
using OpenPhysical.Workstation.Infrastructure.Security;
using OpenPhysical.Workstation.Infrastructure.Verification;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationStateReader
{
    internal static async Task<Result<StudioVerificationState, DesktopError>> ReadAsync(
        IServiceProvider services,
        CancellationToken token
    )
    {
        var paired = await services.GetRequiredService<IPairingStateStore>().ReadAsync(token);
        if (paired.IsFailure)
            return StudioVerificationErrors.From(paired.Error);
        if (paired.Value.HasNoValue || paired.Value.Value.Phase != PairingPhase.Paired)
            return StudioVerificationState.Empty;
        return await ReadPairedAsync(services, paired.Value.Value.CredentialReference, token);
    }

    private static async Task<Result<StudioVerificationState, DesktopError>> ReadPairedAsync(
        IServiceProvider services,
        string credentialReference,
        CancellationToken token
    )
    {
        var operators = await services
            .GetRequiredService<IOperatorIdentityCatalog>()
            .ListAsync(credentialReference, token);
        if (operators.IsFailure)
            return StudioVerificationErrors.From(operators.Error);
        var actor = await services.GetRequiredService<OperatorSessionService>().CurrentAsync(token);
        if (actor.IsFailure)
            return StudioVerificationErrors.From(actor.Error);
        var configuration = await services
            .GetRequiredService<ConfigurationActivationService>()
            .ReadCurrentAsync(token);
        var choices = configuration.IsSuccess
            ? StudioVerificationChoices.Read(configuration.Value)
            : [];
        var clockReady = services
            .GetRequiredService<WorkstationClockConfidence>()
            .Uncertainty.HasValue;
        if (actor.Value.HasNoValue || configuration.IsFailure || !clockReady)
            return new StudioVerificationState(
                true,
                actor.Value.HasValue,
                configuration.IsSuccess,
                operators.Value,
                choices,
                [],
                []
            )
            {
                ClockReady = clockReady,
            };
        return await ReadDevicesAsync(services, operators.Value, choices, token);
    }

    private static async Task<Result<StudioVerificationState, DesktopError>> ReadDevicesAsync(
        IServiceProvider services,
        ImmutableArray<OperatorIdentityOption> operators,
        ImmutableArray<StudioVerificationChoice> choices,
        CancellationToken token
    )
    {
        var readers = await services
            .GetRequiredService<DeviceSessionCoordinator>()
            .ListAsync(token);
        if (readers.IsFailure)
            return StudioVerificationErrors.From(readers.Error);
        var scanners = await services
            .GetRequiredService<WorkstationCaptureDevices>()
            .ListAsync(token);
        if (scanners.IsFailure)
            return StudioVerificationErrors.From(scanners.Error);
        return new StudioVerificationState(
            true,
            true,
            true,
            operators,
            choices,
            readers.Value,
            scanners.Value
        )
        {
            ClockReady = true,
        };
    }
}
