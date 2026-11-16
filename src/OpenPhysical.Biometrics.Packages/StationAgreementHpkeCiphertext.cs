// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

/// <summary>One RFC 9180 base-mode ciphertext for a station agreement challenge.</summary>
/// <param name="Encapsulation">The 65-byte uncompressed P-256 ephemeral point.</param>
/// <param name="Ciphertext">The 32-byte challenge and 16-byte authentication tag.</param>
public sealed record StationAgreementHpkeCiphertext(
    ReadOnlyMemory<byte> Encapsulation,
    ReadOnlyMemory<byte> Ciphertext
);
