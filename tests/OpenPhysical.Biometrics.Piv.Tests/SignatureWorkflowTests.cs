// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
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
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            provider.Requests.Should().HaveCount(2);
            result.Record!.Header.SignatureLength.Should().Be(4);
            CbeffCodec
                .GetSignedData(result.Record)
                .Should()
                .Equal(provider.Requests[^1].SignedContent.ToArray());
            result.Verification!.IdentityBinding.Should().Be(ValidationStatus.Passed);
            record.Header.SignatureLength.Should().Be(0);
            record.Signature.IsEmpty.Should().BeTrue();
        }
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
        result.Failure.Should().Be(SignatureFailure.LengthDidNotConverge);
        provider.Requests.Should().HaveCount(BiometricSignatureWorkflow.MaximumSigningAttempts);
        provider.Verifications.Should().Be(0);
        result.Record.Should().BeNull();
    }

    [TestCase(ValidationStatus.Failed)]
    [TestCase(ValidationStatus.NotEvaluated)]
    public async Task IdentityBindingRequiresVerifiedContent(ValidationStatus signatureStatus)
    {
        var provider = new RecordingProvider { SignatureStatus = signatureStatus };
        var result = await BiometricSignatureWorkflow.SignAsync(
            Record(),
            Identity(),
            provider,
            provider
        );
        result.Failure.Should().Be(SignatureFailure.VerificationFailed);
        result.Verification!.IdentityBinding.Should().Be(ValidationStatus.NotEvaluated);
    }

    [TestCase(ValidationStatus.Failed)]
    [TestCase(ValidationStatus.NotEvaluated)]
    public async Task TrustFindingsStaySeparateFromSignatureIntegrity(ValidationStatus trustStatus)
    {
        var provider = new RecordingProvider { TrustStatus = trustStatus };
        var result = await BiometricSignatureWorkflow.SignAsync(
            Record(),
            Identity(),
            provider,
            provider
        );
        result.IsSuccess.Should().BeTrue();
        result.Verification!.SignatureFindings.Trust.Should().Be(trustStatus);
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
        result.Failure.Should().Be(SignatureFailure.IdentityMismatch);
        result.Record.Should().BeNull();
    }

    [Test]
    public async Task ProviderExceptionsAndCancellationRemainVisibleToTheCaller()
    {
        var provider = new RecordingProvider { ThrowOnSign = true };
        await FluentActions
            .Invoking(async () =>
                await BiometricSignatureWorkflow.SignAsync(Record(), Identity(), provider, provider)
            )
            .Should()
            .ThrowExactlyAsync<IOException>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await FluentActions
            .Invoking(async () =>
                await BiometricSignatureWorkflow.SignAsync(
                    Record(),
                    Identity(),
                    provider,
                    provider,
                    cancellation.Token
                )
            )
            .Should()
            .ThrowExactlyAsync<OperationCanceledException>();
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
        using (new AssertionScope())
        {
            identity.FascN[0].Should().Be(0);
            request.SignedContent[0].Should().Be(3);
            request.Requirements.ContentTypeOid.Should().Be("2.16.840.1.101.3.6.2");
            request.Requirements.SignerDnAttributeOid.Should().Be("2.16.840.1.101.3.6.5");
            request.Requirements.EntryUuidAttributeOid.Should().Be("1.3.6.1.1.16.4");
        }
    }

    [Test]
    public void SignedContentRequiresTheCompleteCanonicalHeader()
    {
        FluentActions
            .Invoking(() =>
                new BiometricSignatureRequest(new byte[CbeffHeader.Size - 1], Identity())
            )
            .Should()
            .ThrowExactly<ArgumentException>();
        var request = new BiometricSignatureRequest(new byte[CbeffHeader.Size], Identity());
        request.SignedContent.Length.Should().Be(CbeffHeader.Size);
    }

    [Test]
    public void AttachmentRejectsLengthChangesAfterSigning()
    {
        FluentActions
            .Invoking(() => BiometricSignatureWorkflow.Attach(Record(), new byte[4]))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Test]
    public async Task SigningRejectsHeaderIdentityMismatchBeforeCallingProvider()
    {
        var fascN = new byte[25];
        fascN[0] = 9;
        var provider = new RecordingProvider();
        await FluentActions
            .Invoking(async () =>
                await BiometricSignatureWorkflow.SignAsync(
                    Record(),
                    new BiometricIdentity(fascN, CardUuid),
                    provider,
                    provider
                )
            )
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
        provider.Requests.Should().BeEmpty();
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
            request.SignedContent.Should().Equal(Requests[^1].SignedContent);
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
