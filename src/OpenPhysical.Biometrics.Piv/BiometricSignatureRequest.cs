// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Contains owned exact content bytes and the identity expected in its signature.</summary>
public sealed class BiometricSignatureRequest
{
    private BiometricSignatureRequest(
        ImmutableArray<byte> signedContent,
        BiometricIdentity identity
    )
    {
        SignedContent = signedContent;
        Identity = identity;
    }

    /// <summary>Gets the exact bytes the external service signs or verifies.</summary>
    public ImmutableArray<byte> SignedContent { get; }

    /// <summary>Gets the expected credential identity.</summary>
    public BiometricIdentity Identity { get; }

    /// <summary>Gets the biometric-specific requirements for the external signature service.</summary>
    public BiometricSignatureRequirements Requirements { get; } = new();

    /// <summary>Creates a request retaining an independent copy of its content.</summary>
    /// <param name="signedContent">The original CBEFF header followed by the biometric record.</param>
    /// <param name="identity">The identity expected in the signed attributes.</param>
    /// <returns>The request, or an error when the content is shorter than a CBEFF header.</returns>
    public static Result<BiometricSignatureRequest, PivError> Create(
        ReadOnlySpan<byte> signedContent,
        BiometricIdentity identity
    ) =>
        signedContent.Length < CbeffHeader.Size
            ? Result.Failure<BiometricSignatureRequest, PivError>(
                new PivError(
                    PivErrorCodes.SignedContentLength,
                    $"Signed content includes the {CbeffHeader.Size}-byte CBEFF header."
                )
            )
            : new BiometricSignatureRequest(
                ImmutableArray.Create(signedContent.ToArray()),
                identity
            );
}
