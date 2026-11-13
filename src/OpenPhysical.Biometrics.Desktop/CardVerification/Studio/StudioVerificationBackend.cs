// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Configuration;
using OpenPhysical.Workstation.Application.Operators;
using OpenPhysical.Workstation.Application.Pairing;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class StudioVerificationBackend(ServiceProvider services)
    : IStudioVerificationBackend
{
    public Task<Result<StudioVerificationState, DesktopError>> ReadAsync(CancellationToken token) =>
        ReadGuardedAsync(token);

    private async Task<Result<StudioVerificationState, DesktopError>> ReadGuardedAsync(
        CancellationToken token
    )
    {
        await using var scope = services.CreateAsyncScope();
        return await StudioVerificationStateReader.ReadAsync(scope.ServiceProvider, token);
    }

    public Task<UnitResult<DesktopError>> PairAsync(
        PairingRequest request,
        CancellationToken token
    ) =>
        ApplyAsync(
            async provider =>
                (await provider.GetRequiredService<PairingService>().PairAsync(request, token)).Map(
                    _ => true
                ),
            token
        );

    public Task<UnitResult<DesktopError>> SignInAsync(string identity, CancellationToken token) =>
        ApplyAsync(
            async provider =>
                (
                    await provider
                        .GetRequiredService<OperatorSessionService>()
                        .SignInAsync(identity, token)
                ).Map(_ => true),
            token
        );

    public Task<UnitResult<DesktopError>> SynchronizeAsync(CancellationToken token) =>
        ApplyAsync(
            async provider =>
                (
                    await provider
                        .GetRequiredService<ConfigurationActivationService>()
                        .SynchronizeAsync(token)
                ).Map(_ => true),
            token
        );

    public Task<UnitResult<DesktopError>> SignOutAsync(CancellationToken token) =>
        ApplyAsync(
            provider =>
            {
                provider.GetRequiredService<OperatorSessionService>().EndLocalSession();
                return Task.FromResult(
                    Result.Success<bool, OpenPhysical.Workstation.Application.WorkstationError>(
                        true
                    )
                );
            },
            token
        );

    public async Task<UnitResult<DesktopError>> UnpairAsync(CancellationToken token)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OperatorSessionService>().EndLocalSession();
        var result = await scope
            .ServiceProvider.GetRequiredService<PairingService>()
            .UnpairAsync(token);
        return result.IsSuccess
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(StudioVerificationErrors.From(result.Error));
    }

    public async Task<Result<CredentialVerificationResult, DesktopError>> VerifyAsync(
        StudioVerificationSelection selection,
        ReadOnlyMemory<byte> pin,
        ReadOnlyMemory<byte> privacyKey,
        Func<
            ImmutableArray<GuidedFingerChoice>,
            CancellationToken,
            Task<Result<GuidedFingerChoice, DesktopError>>
        > choose,
        CancellationToken token
    )
    {
        await using var scope = services.CreateAsyncScope();
        return await StudioVerificationCeremony.RunAsync(
            scope.ServiceProvider,
            selection,
            pin,
            privacyKey,
            choose,
            token
        );
    }

    public async ValueTask DisposeAsync()
    {
        await SignOutAsync(CancellationToken.None);
        await services.DisposeAsync();
    }

    private async Task<UnitResult<DesktopError>> ApplyAsync(
        Func<
            IServiceProvider,
            Task<Result<bool, OpenPhysical.Workstation.Application.WorkstationError>>
        > operation,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
            return UnitResult.Failure(new DesktopError("Cancelled", "Operation cancelled."));
        await using var scope = services.CreateAsyncScope();
        var result = await operation(scope.ServiceProvider);
        return result.IsSuccess
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(StudioVerificationErrors.From(result.Error));
    }
}
