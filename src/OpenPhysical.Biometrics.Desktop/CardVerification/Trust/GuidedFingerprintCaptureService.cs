// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Inspection;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Acquires a qualified sample for authenticated PIV and TWIC reference choices.</summary>
internal sealed class GuidedFingerprintCaptureService(
    FingerprintCeremonyService fingerprints,
    CaptureDriverRegistry devices,
    AcquiredFingerprintAdapter adapter,
    TimeProvider clock
)
{
    internal Task<Result<HolderVerificationEvidence, VerificationError>> CaptureAsync(
        PreparedFingerprintReference prepared,
        CredentialVerificationRequest request,
        ILocalCardSession session,
        string deviceId,
        AcquisitionPolicy policy,
        Func<
            ImmutableArray<FingerprintReferenceSelection>,
            CancellationToken,
            Task<Result<FingerprintReferenceSelection, VerificationError>>
        > guide,
        CancellationToken token
    ) => CaptureCoreAsync(new(prepared, request, session, deviceId, policy, guide), token);

    internal static Result<AcquisitionPolicy, VerificationError> ReadAcquisitionPolicy(
        CredentialVerificationRequest request,
        string deviceId
    ) => FingerprintAcquisitionPolicyReader.Read(request, deviceId);

    private async Task<Result<HolderVerificationEvidence, VerificationError>> CaptureCoreAsync(
        CaptureOperation operation,
        CancellationToken token
    )
    {
        using (operation.Prepared)
        {
            var remaining = operation.Prepared.ExpiresAt - clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
                return Failure();
            var lifetime =
                remaining < operation.Policy.Settings.Lifetime
                    ? remaining
                    : operation.Policy.Settings.Lifetime;
            using var timeout = new CancellationTokenSource(lifetime, clock);
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                timeout.Token
            );
            var selected = await operation
                .Guide(operation.Prepared.Selections, cancelled.Token)
                .ConfigureAwait(false);
            if (selected.IsFailure)
                return selected.Error;
            if (
                !operation.Prepared.Selections.Contains(selected.Value)
                || operation.Session.EnsureCurrent().IsFailure
            )
                return Failure();
            return await CaptureSelectedAsync(operation, selected.Value, cancelled.Token)
                .ConfigureAwait(false);
        }
    }

    private async Task<Result<HolderVerificationEvidence, VerificationError>> CaptureSelectedAsync(
        CaptureOperation operation,
        FingerprintReferenceSelection selection,
        CancellationToken token
    )
    {
        var inventory = await devices.DiscoverAsync(token).ConfigureAwait(false);
        if (inventory.IsFailure)
            return CaptureFailure(inventory.Error);
        var matching = inventory
            .Value.Where(value => value.DeviceId == operation.DeviceId)
            .ToArray();
        if (matching.Length != 1)
            return Failure();
        var opened = await devices
            .OpenAsync(matching[0], operation.Policy.Settings, token)
            .ConfigureAwait(false);
        if (opened.IsFailure)
            return CaptureFailure(opened.Error);
        await using var acquisition = opened.Value;
        return await CaptureFrameAsync(operation, selection, acquisition, token)
            .ConfigureAwait(false);
    }

    private async Task<Result<HolderVerificationEvidence, VerificationError>> CaptureFrameAsync(
        CaptureOperation operation,
        FingerprintReferenceSelection selection,
        IFingerprintCaptureSession acquisition,
        CancellationToken token
    )
    {
        if (operation.Session.EnsureCurrent().IsFailure)
            return Failure();
        var frame = await acquisition.CaptureAsync(token).ConfigureAwait(false);
        if (frame.IsFailure)
            return CaptureFailure(frame.Error);
        if (operation.Session.EnsureCurrent().IsFailure)
            return Failure();
        var capture = adapter.Adapt(
            frame.Value,
            (FingerPosition)selection.Finger,
            operation.Policy.Profile
        );
        if (capture.IsFailure)
            return Failure();
        return await fingerprints
            .ComparePreparedAsync(
                operation.Prepared,
                operation.Request,
                operation.Session,
                capture.Value,
                selection.View,
                token
            )
            .ConfigureAwait(false);
    }

    private static Result<HolderVerificationEvidence, VerificationError> CaptureFailure(
        CaptureError error
    ) =>
        Result.Failure<HolderVerificationEvidence, VerificationError>(
            new(
                error.Code == CaptureErrorCodes.Cancelled
                    ? VerificationErrorCodes.Cancelled
                    : VerificationErrorCodes.EvidenceUnavailable,
                "capture.unavailable"
            )
        );

    private static Result<HolderVerificationEvidence, VerificationError> Failure() =>
        Result.Failure<HolderVerificationEvidence, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "capture.unavailable")
        );

    internal sealed record AcquisitionPolicy(
        FingerprintAcquisitionProfile Profile,
        CaptureSettings Settings
    );

    private sealed record CaptureOperation(
        PreparedFingerprintReference Prepared,
        CredentialVerificationRequest Request,
        ILocalCardSession Session,
        string DeviceId,
        AcquisitionPolicy Policy,
        Func<
            ImmutableArray<FingerprintReferenceSelection>,
            CancellationToken,
            Task<Result<FingerprintReferenceSelection, VerificationError>>
        > Guide
    );
}
