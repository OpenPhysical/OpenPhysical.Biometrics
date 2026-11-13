// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Workstation.Application.Verification;

/// <summary>Guided credential kind.</summary>
public enum GuidedCredentialKind
{
    /// <summary>PIV card with an authenticated fingerprint reference.</summary>
    Piv = 1,

    /// <summary>TWIC with generation-specific privacy and cancellation checks.</summary>
    Twic = 2,
}

/// <summary>Guided verification phase.</summary>
public enum GuidedVerificationPhase
{
    /// <summary>Authenticating the card reference.</summary>
    Preparing = 1,

    /// <summary>Waiting for an explicit enrolled-finger selection.</summary>
    AwaitingFinger = 2,

    /// <summary>Acquiring the selected finger.</summary>
    Capturing = 3,

    /// <summary>Verification finished with separate check results.</summary>
    Completed = 4,

    /// <summary>Required evidence or capture failed.</summary>
    Failed = 5,

    /// <summary>Draining work before releasing resources.</summary>
    Cancelling = 6,
}

/// <summary>Guided finger choice.</summary>
/// <param name="Finger">Finger.</param>
/// <param name="View">View.</param>
public readonly record struct GuidedFingerChoice(byte Finger, byte View);

/// <summary>Guided verification request.</summary>
/// <param name="ReaderId">Reader id.</param>
/// <param name="InterfaceType">Interface type.</param>
/// <param name="CaptureDeviceId">Capture device id.</param>
/// <param name="Kind">Kind.</param>
/// <param name="ProfileId">Profile id.</param>
/// <param name="MarsecLevel">Marsec level.</param>
/// <param name="IssuerCertificates">Issuer certificates.</param>
/// <param name="ContentSignerCertificates">Content signer certificates.</param>
public sealed record GuidedVerificationRequest(
    Guid ReaderId,
    PivInterfaceType InterfaceType,
    string CaptureDeviceId,
    GuidedCredentialKind Kind,
    string ProfileId,
    int MarsecLevel,
    ImmutableArray<ImmutableArray<byte>> IssuerCertificates,
    ImmutableArray<ImmutableArray<byte>> ContentSignerCertificates
);

/// <summary>Guided verification status.</summary>
/// <param name="OperationId">Operation id.</param>
/// <param name="Phase">Phase.</param>
/// <param name="ExpiresAt">Expires at.</param>
/// <param name="BundleSha256">Bundle sha256.</param>
/// <param name="Choices">Choices.</param>
/// <param name="Result">Result.</param>
/// <param name="Error">Error.</param>
public sealed record GuidedVerificationStatus(
    Guid OperationId,
    GuidedVerificationPhase Phase,
    DateTimeOffset ExpiresAt,
    string BundleSha256,
    ImmutableArray<GuidedFingerChoice> Choices,
    Maybe<CredentialVerificationResult> Result,
    Maybe<VerificationErrorCodes> Error
);

/// <summary>Capture-side worker that retains the same card through guidance and comparison.</summary>
public interface IGuidedVerificationRunner
{
    /// <summary>Runs authenticated card and holder checks with explicit finger selection.</summary>
    /// <param name="request">Bounded reader, scanner and signed workflow selection.</param>
    /// <param name="bundle">Authenticated signed configuration.</param>
    /// <param name="pin">Caller-owned protected-reference access PIN.</param>
    /// <param name="privacyKey">Caller-owned separately acquired TWIC privacy key.</param>
    /// <param name="guidance">Selection among authenticated enrolled fingers.</param>
    /// <param name="token">Cancellation for this operation.</param>
    /// <returns>Separate verification checks or a typed failure.</returns>
    Task<Result<CredentialVerificationResult, VerificationError>> VerifyAsync(
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
    );
}
