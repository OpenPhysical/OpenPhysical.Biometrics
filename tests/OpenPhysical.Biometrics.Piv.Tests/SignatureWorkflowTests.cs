// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;

internal sealed class SignatureWorkflowTests
{
    private static readonly Guid CardUuid = Guid.Parse("48a735cd-b680-40d0-aa5d-6d37c72af068");

    [Test]
    public async Task LengthConvergenceKeepsExactlyTheContentThatWasSigned()
    {
        var identity = new BiometricIdentity(new byte[25], CardUuid);
        var record = Record();
        var provider = new RecordingProvider();
        var result = await BiometricSignatureWorkflow.SignAsync(
            record,
            identity,
            provider,
            provider
        );
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            Assert.That(result.Record!.Header.SignatureLength, Is.EqualTo(4));
            Assert.That(
                CbeffCodec.GetSignedData(result.Record),
                Is.EqualTo(provider.Requests[^1].SignedContent.ToArray())
            );
            Assert.That(result.Verification!.IdentityBinding, Is.EqualTo(ValidationStatus.Passed));
            Assert.That(record.Header.SignatureLength, Is.Zero);
            Assert.That(record.Signature.IsEmpty, Is.True);
        });
    }

    [Test]
    public async Task VariableLengthsReachAnExplicitAttemptLimit()
    {
        var identity = new BiometricIdentity(new byte[25], CardUuid);
        var provider = new RecordingProvider { AlternateLength = true };
        var result = await BiometricSignatureWorkflow.SignAsync(
            Record(),
            identity,
            provider,
            provider
        );
        Assert.That(result.Failure, Is.EqualTo(SignatureFailure.LengthDidNotConverge));
        Assert.That(
            provider.Requests,
            Has.Count.EqualTo(BiometricSignatureWorkflow.MaximumSigningAttempts)
        );
        Assert.That(provider.Verifications, Is.Zero);
        Assert.That(result.Record, Is.Null);
    }

    [TestCase(ValidationStatus.Failed)]
    [TestCase(ValidationStatus.NotEvaluated)]
    public async Task UnverifiedContentNeverAcquiresAnIdentityBinding(
        ValidationStatus signatureStatus
    )
    {
        var provider = new RecordingProvider { SignatureStatus = signatureStatus };
        var result = await BiometricSignatureWorkflow.SignAsync(
            Record(),
            Identity(),
            provider,
            provider
        );
        Assert.That(result.Failure, Is.EqualTo(SignatureFailure.VerificationFailed));
        Assert.That(
            result.Verification!.IdentityBinding,
            Is.EqualTo(ValidationStatus.NotEvaluated)
        );
    }

    [Test]
    public async Task TrustFindingsStaySeparateFromSignatureIntegrity()
    {
        var provider = new RecordingProvider { TrustStatus = ValidationStatus.Failed };
        var result = await BiometricSignatureWorkflow.SignAsync(
            Record(),
            Identity(),
            provider,
            provider
        );
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(
            result.Verification!.SignatureFindings.Trust,
            Is.EqualTo(ValidationStatus.Failed)
        );
    }

    [Test]
    public async Task AuthenticatedIdentityMustMatchTheExpectedCard()
    {
        var provider = new RecordingProvider { WrongIdentity = true };
        var result = await BiometricSignatureWorkflow.SignAsync(
            Record(),
            Identity(),
            provider,
            provider
        );
        Assert.That(result.Failure, Is.EqualTo(SignatureFailure.IdentityMismatch));
        Assert.That(result.Record, Is.Null);
    }

    [Test]
    public void ProviderExceptionsAndCancellationRemainVisibleToTheCaller()
    {
        var provider = new RecordingProvider { ThrowOnSign = true };
        Assert.ThrowsAsync<IOException>(async () =>
            await BiometricSignatureWorkflow.SignAsync(Record(), Identity(), provider, provider)
        );
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await BiometricSignatureWorkflow.SignAsync(
                Record(),
                Identity(),
                provider,
                provider,
                cancellation.Token
            )
        );
    }

    [Test]
    public void RequestsAndIdentityOwnTheirBuffersAndDeclareCorrectProfileOids()
    {
        var identityBytes = new byte[25];
        var identity = new BiometricIdentity(identityBytes, CardUuid);
        var bytes = CbeffCodec.GetSignedData(Record());
        var request = new BiometricSignatureRequest(bytes, identity);
        identityBytes[0] = 9;
        bytes[0] = 9;
        Assert.Multiple(() =>
        {
            Assert.That(identity.FascN[0], Is.Zero);
            Assert.That(request.SignedContent[0], Is.EqualTo(3));
            Assert.That(request.Requirements.ContentTypeOid, Is.EqualTo("2.16.840.1.101.3.6.2"));
            Assert.That(
                request.Requirements.SignerDnAttributeOid,
                Is.EqualTo("2.16.840.1.101.3.6.5")
            );
            Assert.That(request.Requirements.EntryUuidAttributeOid, Is.EqualTo("1.3.6.1.1.16.4"));
        });
    }

    [Test]
    public void AttachmentRejectsLengthChangesAfterSigning()
    {
        Assert.Throws<ArgumentException>(() =>
            BiometricSignatureWorkflow.Attach(Record(), new byte[4])
        );
    }

    [Test]
    public void SigningRejectsHeaderIdentityMismatchBeforeCallingProvider()
    {
        var fascN = new byte[25];
        fascN[0] = 9;
        var provider = new RecordingProvider();
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await BiometricSignatureWorkflow.SignAsync(
                Record(),
                new BiometricIdentity(fascN, CardUuid),
                provider,
                provider
            )
        );
        Assert.That(provider.Requests, Is.Empty);
    }

    private static BiometricIdentity Identity() => new(new byte[25], CardUuid);

    private static CbeffRecord Record()
    {
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var header = CbeffHeader.Create(
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
        );
        return new(header, ImmutableArray.Create<byte>(1, 2, 3), ImmutableArray<byte>.Empty);
    }

    // This records the boundary contract. Cryptographic adapter tests live with
    // the external provider that constructs CMS and validates certificate trust.
    private sealed class RecordingProvider
        : IBiometricSignatureProvider,
            IBiometricSignatureVerifier
    {
        internal List<BiometricSignatureRequest> Requests { get; } = [];
        internal bool AlternateLength { get; init; }
        internal bool ThrowOnSign { get; init; }
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
            Assert.That(request.SignedContent, Is.EqualTo(Requests[^1].SignedContent));
            var identity = WrongIdentity
                ? new BiometricIdentity(
                    new byte[25],
                    Guid.Parse("37f65fca-95c1-40b1-8f59-124a6c7ae694")
                )
                : request.Identity;
            return Task.FromResult(
                new BiometricSignatureFindings(
                    SignatureStatus,
                    ValidationStatus.Passed,
                    TrustStatus,
                    ValidationStatus.NotEvaluated,
                    identity
                )
            );
        }
    }
}
