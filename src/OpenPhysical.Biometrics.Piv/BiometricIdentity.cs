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
    ) =>
        WithUuid(fascN, cardUuid)
            .Ensure(
                identity => identity.CardUuid != Guid.Empty,
                new PivError(PivErrorCodes.InvalidCardUuid, "A card UUID must be specified.")
            );

    /// <summary>Represents an existing reference whose external credential profile explicitly omits the UUID.</summary>
    /// <param name="fascN">The complete 25-byte FASC-N authenticated by the credential workflow.</param>
    /// <returns>An identity with an absent UUID, or a typed invalid-FASC-N failure.</returns>
    /// <remarks>The caller authorizes this exception, such as Legacy TWIC. New PIV issuance uses Create.</remarks>
    public static Result<BiometricIdentity, PivError> CreateWithoutCardUuid(
        ReadOnlySpan<byte> fascN
    ) => WithUuid(fascN, Guid.Empty);

    private static Result<BiometricIdentity, PivError> WithUuid(
        ReadOnlySpan<byte> fascN,
        Guid cardUuid
    ) =>
        fascN.Length != 25
            ? Result.Failure<BiometricIdentity, PivError>(
                new PivError(PivErrorCodes.InvalidFascN, "A PIV FASC-N contains 25 bytes.")
            )
            : new BiometricIdentity(ImmutableArray.Create(fascN.ToArray()), cardUuid);

    /// <summary>Supplies the encoded values that define identity equality.</summary>
    /// <returns>The card UUID followed by each FASC-N byte.</returns>
    protected override IEnumerable<IComparable> GetEqualityComponents() =>
        FascN.Select(value => (IComparable)value).Prepend(CardUuid);
}
