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

internal static class GuidedVerificationInputs
{
    internal static bool IsValid(GuidedVerificationRequest request) =>
        request is not null
        && request.ReaderId != Guid.Empty
        && request.InterfaceType is PivInterfaceType.Contact or PivInterfaceType.Contactless
        && Enum.IsDefined(request.Kind)
        && !string.IsNullOrWhiteSpace(request.CaptureDeviceId)
        && request.CaptureDeviceId.Length <= 256
        && !string.IsNullOrWhiteSpace(request.ProfileId)
        && request.ProfileId.Length <= 256
        && (request.Kind != GuidedCredentialKind.Twic || request.MarsecLevel is >= 1 and <= 3)
        && Bounded(request.IssuerCertificates)
        && Bounded(request.ContentSignerCertificates);

    private static bool Bounded(ImmutableArray<ImmutableArray<byte>> values) =>
        !values.IsDefault
        && values.Length <= 10
        && values.All(value => !value.IsDefaultOrEmpty && value.Length <= 65536);
}
