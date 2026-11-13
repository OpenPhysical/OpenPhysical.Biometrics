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

internal sealed record TwicLiveVerificationInput(
    ILocalCardSession Session,
    CredentialVerificationRequest Request,
    TwicAcceptanceProfile Profile,
    int MarsecLevel,
    Maybe<TwicPrivacyKeyContainer> PrivacyKey,
    string DeviceId,
    Func<
        ImmutableArray<FingerprintReferenceSelection>,
        CancellationToken,
        Task<Result<FingerprintReferenceSelection, VerificationError>>
    > Guide,
    CancellationToken Token
)
{
    internal long Generation { get; } = Session.Generation;
    internal bool ValidContext =>
        Session.Generation == Generation
        && Session.EnsureCurrent().IsSuccess
        && Session.Card.Mode == ConnectionMode.Exclusive
        && MarsecLevel is >= 1 and <= 3
        && Request.Workflow == CredentialVerificationWorkflow.Twic
        && TwicAcceptanceService.AuthenticatedProfile(Request.Bundle, Profile);
}
