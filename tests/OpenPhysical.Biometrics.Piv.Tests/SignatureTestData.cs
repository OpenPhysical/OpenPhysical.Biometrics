// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Cbeff;

internal static class SignatureTestData
{
    internal static readonly Guid CardUuid = Guid.Parse("48a735cd-b680-40d0-aa5d-6d37c72af068");

    internal static BiometricIdentity Identity() =>
        BiometricIdentity.Create(new byte[25], CardUuid).Value;

    internal static BiometricIdentity IdentityWithFascN(byte first)
    {
        var fascN = new byte[25];
        fascN[0] = first;
        return BiometricIdentity.Create(fascN, CardUuid).Value;
    }

    internal static CbeffRecord Record()
    {
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var header = CbeffHeader
            .Create(
                3,
                0,
                0x001B,
                7,
                now,
                now,
                now.AddYears(1),
                8,
                0x80,
                80,
                new byte[18],
                new byte[25],
                securityOptions: 0x0D
            )
            .Value;
        return new(header, ImmutableArray.Create<byte>(1, 2, 3), ImmutableArray<byte>.Empty);
    }
}
