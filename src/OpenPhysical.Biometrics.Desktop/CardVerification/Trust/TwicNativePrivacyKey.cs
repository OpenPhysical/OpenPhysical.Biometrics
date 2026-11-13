// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.Middleware.Diagnostics;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.CredentialSnapshots;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Twic.Codec;
using ApplicationId = OpenPhysical.CardEdge.Application.ApplicationId;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

internal static class TwicNativePrivacyKey
{
    internal static Result<TwicPrivacyKeyContainer, VerificationError> Read(
        TwicLiveSession live,
        CancellationToken token
    )
    {
        var card = Maybe<ICard>.From(live.Session.Card);
        if (
            live.Session.InterfaceType != PivInterfaceType.Contact
            || PivVectorCaptureScope.Policy == CardDiagnosticPolicy.TestVectors
            || TwicMiddleware.TwicSelectApplication(card, out var selected) != PivStatusWord.PivOk
            || selected.Model.HasNoValue
            || selected.Model.Value != live.Profile.Generation
        )
            return Failed("twic.privacy-key-unavailable");
        token.ThrowIfCancellationRequested();
        var definition = TwicDataObjectRegistry
            .GetDefinitions(live.Profile.Generation)
            .Single(value => value.Name == "TWIC Privacy Key (TPK) Container");
        if (!live.IsCurrent)
            return Failed("twic.session-changed");
        return ReadContainer(live, definition);
    }

    private static Result<TwicPrivacyKeyContainer, VerificationError> ReadContainer(
        TwicLiveSession live,
        TwicDataObjectDefinition definition
    )
    {
        var status = TwicMiddleware.TwicGetData(
            Maybe<ICard>.From(live.Session.Card),
            definition,
            1024,
            out var raw
        );
        try
        {
            if (status != PivStatusWord.PivOk)
                return Failed("twic.privacy-key-unavailable");
            var parsed = TwicPrivacyKeyContainerCodec.FromBytes(raw);
            if (parsed.IsFailure)
                return Failed("twic.privacy-key-unavailable");
            if (live.IsCurrent)
                return parsed.Value;
            FingerprintReferenceCryptography.Clear(parsed.Value.Key);
            return Failed("twic.session-changed");
        }
        finally
        {
            FingerprintReferenceCryptography.Clear(raw);
        }
    }

    private static Result<TwicPrivacyKeyContainer, VerificationError> Failed(string reason) =>
        Result.Failure<TwicPrivacyKeyContainer, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, reason)
        );
}
