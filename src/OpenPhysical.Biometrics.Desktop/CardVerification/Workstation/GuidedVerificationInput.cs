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

internal sealed record GuidedVerificationInput(
    GuidedVerificationRequest Request,
    VerifiedBundleSnapshot Bundle,
    ReadOnlyMemory<byte> Pin,
    ReadOnlyMemory<byte> PrivacyKey,
    Func<
        ImmutableArray<GuidedFingerChoice>,
        CancellationToken,
        Task<Result<GuidedFingerChoice, VerificationError>>
    > Guidance,
    CancellationToken Token
)
{
    internal async Task<Result<FingerprintReferenceSelection, VerificationError>> GuideAsync(
        ImmutableArray<FingerprintReferenceSelection> choices,
        CancellationToken token
    )
    {
        var selected = await Guidance(
                choices
                    .Select(value => new GuidedFingerChoice(value.Finger, value.View))
                    .ToImmutableArray(),
                token
            )
            .ConfigureAwait(false);
        return selected.IsFailure
            ? Result.Failure<FingerprintReferenceSelection, VerificationError>(selected.Error)
            : new FingerprintReferenceSelection(selected.Value.Finger, selected.Value.View);
    }
}
