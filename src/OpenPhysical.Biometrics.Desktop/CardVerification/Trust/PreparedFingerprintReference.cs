// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Inspection;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>A finger and view authenticated from the card's signed reference.</summary>
/// <param name="Finger">INCITS finger position.</param>
/// <param name="View">View number within that finger's reference.</param>
public readonly record struct FingerprintReferenceSelection(byte Finger, byte View);

/// <summary>Authority-local, single-use preparation for guided capture on one retained card lease.</summary>
public sealed class PreparedFingerprintReference : IDisposable
{
    private readonly FingerprintCeremonyService owner;
    private readonly ILocalCardSession session;
    private readonly long generation;
    private readonly CredentialVerificationRequest request;
    private readonly DateTimeOffset preparedAt;
    private int consumed;
    private int disposed;

    internal PreparedFingerprintReference(
        FingerprintCeremonyService owner,
        ILocalCardSession session,
        CredentialVerificationRequest request,
        DateTimeOffset preparedAt,
        DateTimeOffset expiresAt,
        ImmutableArray<FingerprintReferenceSelection> selections,
        ImmutableArray<byte> chuid,
        ImmutableArray<byte> fingerprint,
        ImmutableArray<X509Certificate> chain,
        bool twic = false
    )
    {
        this.owner = owner;
        this.session = session;
        generation = session.Generation;
        this.request = request;
        this.preparedAt = preparedAt;
        ExpiresAt = expiresAt;
        Selections = selections;
        Chuid = ImmutableCollectionsMarshal.AsImmutableArray(chuid.ToArray());
        Fingerprint = ImmutableCollectionsMarshal.AsImmutableArray(fingerprint.ToArray());
        Twic = twic;
        Chain = chain;
    }

    /// <summary>Authenticated choices displayed before acquiring a finger.</summary>
    public ImmutableArray<FingerprintReferenceSelection> Selections { get; }

    /// <summary>Exclusive upper validity bound; comparison rechecks status and freshness.</summary>
    public DateTimeOffset ExpiresAt { get; }
    internal bool Twic { get; }
    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;
    internal DateTimeOffset PreparedAt => preparedAt;
    internal ImmutableArray<byte> Chuid { get; }
    internal ImmutableArray<byte> Fingerprint { get; }
    internal ImmutableArray<X509Certificate> Chain { get; }

    internal bool Consume(
        FingerprintCeremonyService caller,
        ILocalCardSession currentSession,
        CredentialVerificationRequest current,
        DateTimeOffset now
    )
    {
        return !IsDisposed
            && ReferenceEquals(owner, caller)
            && ReferenceEquals(session, currentSession)
            && session.Generation == generation
            && session.EnsureCurrent().IsSuccess
            && now >= preparedAt
            && now < ExpiresAt
            && current.CredentialSha256 == request.CredentialSha256
            && current.ProfileId == request.ProfileId
            && current.Workflow == request.Workflow
            && current.Bundle.ManifestSha256 == request.Bundle.ManifestSha256
            && current.ClockUncertainty == request.ClockUncertainty
            && current.Offline == request.Offline
            && current.Lifecycle == request.Lifecycle
            && Interlocked.CompareExchange(ref consumed, 1, 0) == 0;
    }

    internal Task<Result<HolderVerificationEvidence, VerificationError>> CompareAsync(
        FingerprintComparisonCeremony matching,
        CredentialVerificationRequest request,
        FingerprintCapture capture,
        byte viewNumber,
        CancellationToken token
    ) =>
        matching.CompareCoreAsync(
            request,
            Chuid,
            Fingerprint,
            Chain,
            capture,
            viewNumber,
            Twic,
            Maybe<FingerprintComparisonCeremony.AttendedOperatorAuthorization>.None,
            token
        );

    /// <summary>Clears owned reference bytes when guidance finishes, expires or is cancelled.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var chuid = ImmutableCollectionsMarshal.AsArray(Chuid);
        var fingerprint = ImmutableCollectionsMarshal.AsArray(Fingerprint);
        if (chuid is not null)
            Array.Clear(chuid);
        if (fingerprint is not null)
            Array.Clear(fingerprint);
    }
}
