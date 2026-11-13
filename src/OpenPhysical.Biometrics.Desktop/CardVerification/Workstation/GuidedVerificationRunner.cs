// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Middleware.Twic.Codec;
using OpenPhysical.Workstation.Application.Devices;
using OpenPhysical.Workstation.Application.Verification;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Workstation.Infrastructure.Verification;

/// <summary>Guided verification runner.</summary>
/// <param name="devices">Configured capture drivers.</param>
/// <param name="piv">Piv.</param>
/// <param name="twic">Twic.</param>
/// <param name="confidence">Confidence.</param>
/// <param name="clock">Trusted host clock.</param>
public sealed class GuidedVerificationRunner(
    DeviceSessionCoordinator devices,
    LiveFingerprintVerificationService piv,
    LiveTwicFingerprintVerificationService twic,
    WorkstationClockConfidence confidence,
    TimeProvider clock
) : IGuidedVerificationRunner
{
    /// <summary>Verify async.</summary>
    /// <param name="request">Authenticated credential and workflow context.</param>
    /// <param name="bundle">Authenticated signed configuration.</param>
    /// <param name="pin">Caller-owned PIN bytes.</param>
    /// <param name="privacyKey">Caller-owned TWIC privacy key.</param>
    /// <param name="guidance">Explicit selection among authenticated enrolled fingers.</param>
    /// <param name="token">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public Task<Result<CredentialVerificationResult, VerificationError>> VerifyAsync(
        GuidedVerificationRequest request,
        VerifiedBundleSnapshot bundle,
        ReadOnlyMemory<byte> pin,
        ReadOnlyMemory<byte> privacyKey,
        Func<
            ImmutableArray<GuidedFingerChoice>,
            CancellationToken,
            Task<Result<GuidedFingerChoice, VerificationError>>
        > guidance,
        CancellationToken token
    ) =>
        new GuidedCardVerificationReader(devices, piv, twic, confidence, clock).RunAsync(
            new GuidedVerificationInput(request, bundle, pin, privacyKey, guidance, token)
        );
}
