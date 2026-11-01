// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Identifies a credential using the values bound into PIV biometric signatures.</summary>
public sealed class BiometricIdentity : ValueObject
{
    private BiometricIdentity(ImmutableArray<byte> fascN, Guid cardUuid)
    {
        FascN = fascN;
        CardUuid = cardUuid;
    }

    /// <summary>Gets the owned encoded FASC-N.</summary>
    public ImmutableArray<byte> FascN { get; }

    /// <summary>Gets the card UUID.</summary>
    public Guid CardUuid { get; }

    /// <summary>Creates an owned, validated identity.</summary>
    /// <param name="fascN">The 25-byte encoded FASC-N.</param>
    /// <param name="cardUuid">The card UUID from the credential's CHUID.</param>
    /// <returns>The identity, or an error when the FASC-N length or UUID is invalid.</returns>
    public static Result<BiometricIdentity, PivError> Create(
        ReadOnlySpan<byte> fascN,
        Guid cardUuid
    )
    {
        if (fascN.Length != 25)
            return Result.Failure<BiometricIdentity, PivError>(
                new PivError(PivErrorCodes.InvalidFascN, "A PIV FASC-N contains 25 bytes.")
            );
        return cardUuid == Guid.Empty
            ? Result.Failure<BiometricIdentity, PivError>(
                new PivError(PivErrorCodes.InvalidCardUuid, "A card UUID must be specified.")
            )
            : new BiometricIdentity(ImmutableArray.Create(fascN.ToArray()), cardUuid);
    }

    /// <summary>Supplies the encoded values that define identity equality.</summary>
    /// <returns>The card UUID followed by each FASC-N byte.</returns>
    protected override IEnumerable<IComparable> GetEqualityComponents() =>
        FascN.Select(value => (IComparable)value).Prepend(CardUuid);
}
