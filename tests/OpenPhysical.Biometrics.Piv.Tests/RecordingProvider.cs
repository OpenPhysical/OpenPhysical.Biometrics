// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;

// This records the boundary contract. Cryptographic adapter tests live with
// the external provider that constructs CMS and validates certificate trust.
internal sealed class RecordingProvider : IBiometricSignatureProvider, IBiometricSignatureVerifier
{
    internal List<BiometricSignatureRequest> Requests { get; } = [];
    internal bool AlternateLength { get; init; }
    internal bool ThrowOnSign { get; init; }
    internal bool ThrowOnVerify { get; init; }
    internal bool WrongIdentity { get; init; }
    internal ValidationStatus SignatureStatus { get; init; } = ValidationStatus.Passed;
    internal ValidationStatus TrustStatus { get; init; } = ValidationStatus.Passed;
    internal int Verifications { get; private set; }

    public Task<ImmutableArray<byte>> SignAsync(
        BiometricSignatureRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (ThrowOnSign)
            throw new IOException("External signing service failed.");
        Requests.Add(request);
        return Task.FromResult(
            ImmutableArray.Create(new byte[AlternateLength ? 4 + (Requests.Count % 2) : 4])
        );
    }

    public Task<BiometricSignatureFindings> VerifyAsync(
        BiometricSignatureRequest request,
        ImmutableArray<byte> signatureBlock,
        CancellationToken cancellationToken = default
    )
    {
        Verifications++;
        if (ThrowOnVerify)
            throw new IOException("External verification service failed.");
        request.SignedContent.Should().Equal(Requests[^1].SignedContent);
        var identity = WrongIdentity
            ? BiometricIdentity
                .Create(new byte[25], Guid.Parse("37f65fca-95c1-40b1-8f59-124a6c7ae694"))
                .Value
            : request.Identity;
        return Task.FromResult(
            new BiometricSignatureFindings(
                SignatureStatus,
                ValidationStatus.Passed,
                TrustStatus,
                ValidationStatus.NotEvaluated,
                Maybe.From(identity)
            )
        );
    }
}
