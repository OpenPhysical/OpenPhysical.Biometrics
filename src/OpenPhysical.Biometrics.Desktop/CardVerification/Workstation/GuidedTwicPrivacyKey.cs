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

internal sealed class GuidedTwicPrivacyKey : IDisposable
{
    private GuidedTwicPrivacyKey(Maybe<TwicPrivacyKeyContainer> value) => Value = value;

    internal Maybe<TwicPrivacyKeyContainer> Value { get; }

    internal static Result<GuidedTwicPrivacyKey, VerificationError> Parse(
        ReadOnlyMemory<byte> input
    )
    {
        if (input.IsEmpty)
            return new GuidedTwicPrivacyKey(Maybe<TwicPrivacyKeyContainer>.None);
        var raw = input.ToArray();
        try
        {
            var parsed = TwicPrivacyKeyContainerCodec.FromBytes(
                ImmutableCollectionsMarshal.AsImmutableArray(raw)
            );
            return parsed.IsFailure
                ? WorkstationVerificationEvidence.Failure<GuidedTwicPrivacyKey>()
                : new GuidedTwicPrivacyKey(parsed.Value);
        }
        finally
        {
            Array.Clear(raw);
        }
    }

    public void Dispose()
    {
        if (Value.HasValue)
        {
            var owned = ImmutableCollectionsMarshal.AsArray(Value.Value.Key);
            if (owned is not null)
                Array.Clear(owned);
        }
    }
}
