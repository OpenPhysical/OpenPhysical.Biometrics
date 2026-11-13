// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Workstation.Application.Verification;

internal sealed class GuidedVerificationOperation(
    Guid id,
    DateTimeOffset expiresAt,
    string bundleSha256,
    TimeProvider clock
)
{
    internal Guid Id { get; } = id;
    internal string BundleSha256 { get; } = bundleSha256;
    internal DateTimeOffset ExpiresAt { get; } = expiresAt;
    internal CancellationTokenSource Cancellation { get; } = new(TimeSpan.FromMinutes(3), clock);
    internal Task Work { get; set; } = Task.CompletedTask;
    internal TaskCompletionSource<GuidedFingerChoice> Selection { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal GuidedVerificationPhase Phase { get; set; } = GuidedVerificationPhase.Preparing;
    internal ImmutableArray<GuidedFingerChoice> Choices { get; set; } =
        ImmutableArray<GuidedFingerChoice>.Empty;
    internal Maybe<CredentialVerificationResult> Result { get; set; }
    internal Maybe<VerificationErrorCodes> Error { get; set; }
}
