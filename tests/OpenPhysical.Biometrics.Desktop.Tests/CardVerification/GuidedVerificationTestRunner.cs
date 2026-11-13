// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Tests;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Workstation.Tests;

internal sealed class GuidedVerificationTestRunner : IGuidedVerificationRunner
{
    internal TaskCompletionSource Ready { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool Block { get; init; }
    internal bool Captured { get; private set; }
    internal int Invocations { get; private set; }
    internal ReadOnlyMemory<byte> Pin { get; private set; }

    public async Task<Result<CredentialVerificationResult, VerificationError>> VerifyAsync(
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
    )
    {
        Invocations++;
        Pin = pin;
        if (Block)
        {
            Ready.TrySetResult();
            await Release.Task;
        }
        else
        {
            var selection = guidance(ImmutableArray.Create(new GuidedFingerChoice(2, 0)), token);
            Ready.TrySetResult();
            var selected = await selection;
            Captured = selected.IsSuccess;
        }
        return Result.Failure<CredentialVerificationResult, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "fixture.no-matching-engine")
        );
    }
}
