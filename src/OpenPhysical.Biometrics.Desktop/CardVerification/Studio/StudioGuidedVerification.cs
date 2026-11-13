// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioGuidedVerification
{
    internal static async Task<Result<CredentialVerificationResult, DesktopError>> WaitAsync(
        GuidedVerificationCoordinator operation,
        Guid id,
        Func<
            ImmutableArray<GuidedFingerChoice>,
            CancellationToken,
            Task<Result<GuidedFingerChoice, DesktopError>>
        > choose,
        CancellationToken token
    )
    {
        while (!token.IsCancellationRequested)
        {
            var status = operation.Read(id);
            if (status.HasNoValue)
                return new DesktopError(
                    "Verification.Expired",
                    "This card check expired. Start a new check."
                );
            if (status.Value.Result.HasValue)
                return status.Value.Result.Value;
            if (status.Value.Error.HasValue)
                return StudioVerificationErrors.From(
                    new VerificationError(status.Value.Error.Value, "guidance.unavailable")
                );
            if (status.Value.Phase == GuidedVerificationPhase.AwaitingFinger)
            {
                var selected = await choose(status.Value.Choices, token);
                if (selected.IsFailure)
                    return selected.Error;
                var admitted = operation.Select(id, selected.Value);
                if (admitted.IsFailure)
                    return StudioVerificationErrors.From(admitted.Error);
            }
            await Task.Delay(100, token);
        }
        operation.Cancel(id);
        return new DesktopError("Cancelled", "Card check cancelled.");
    }
}
