// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Cbeff;

// The explicit scalar fields of a PIV patron header, before byte encoding.
internal sealed record CbeffHeaderFields(
    byte PatronHeaderVersion,
    byte SecurityOptions,
    uint BiometricDataLength,
    ushort SignatureLength,
    ushort FormatOwner,
    ushort FormatType,
    DateTimeOffset CreationDate,
    DateTimeOffset ValidityNotBefore,
    DateTimeOffset ValidityNotAfter,
    uint BiometricType,
    byte BiometricDataType,
    sbyte Quality
);
