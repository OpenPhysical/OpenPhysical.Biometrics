// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;

internal sealed class SignatureWorkflowTests
{
    [Test]
    public async Task LengthConvergenceKeepsExactlyTheContentThatWasSigned()
    {
        var record = SignatureTestData.Record();
        var provider = new RecordingProvider();
        var result = (
            await BiometricSignatureWorkflow.SignAsync(
                record,
                SignatureTestData.Identity(),
                provider,
                provider
            )
        ).Value;
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            provider.Requests.Should().HaveCount(2);
            result.Record.Value.Header.SignatureLength.Should().Be(4);
            CbeffCodec
                .GetSignedData(result.Record.Value)
                .Value.Should()
                .Equal(provider.Requests[^1].SignedContent.ToArray());
            result.Verification.Value.IdentityBinding.Should().Be(ValidationStatus.Passed);
            record.Header.SignatureLength.Should().Be(0);
            record.Signature.IsEmpty.Should().BeTrue();
        }
    }

    [Test]
    public async Task VariableLengthsReachAnExplicitAttemptLimit()
    {
        var provider = new RecordingProvider { AlternateLength = true };
        var result = (
            await BiometricSignatureWorkflow.SignAsync(
                SignatureTestData.Record(),
                SignatureTestData.Identity(),
                provider,
                provider
            )
        ).Value;
        using (new AssertionScope())
        {
            result.Failure.Should().Be(SignatureFailure.LengthDidNotConverge);
            provider.Requests.Should().HaveCount(BiometricSignatureWorkflow.MaximumSigningAttempts);
            provider.Verifications.Should().Be(0);
            result.Record.HasNoValue.Should().BeTrue();
        }
    }

    [TestCase(ValidationStatus.Failed)]
    [TestCase(ValidationStatus.NotEvaluated)]
    public async Task IdentityBindingRequiresVerifiedContent(ValidationStatus signatureStatus)
    {
        var provider = new RecordingProvider { SignatureStatus = signatureStatus };
        var result = (
            await BiometricSignatureWorkflow.SignAsync(
                SignatureTestData.Record(),
                SignatureTestData.Identity(),
                provider,
                provider
            )
        ).Value;
        result.Failure.Should().Be(SignatureFailure.VerificationFailed);
        result.Verification.Value.IdentityBinding.Should().Be(ValidationStatus.NotEvaluated);
    }

    [TestCase(ValidationStatus.Failed)]
    [TestCase(ValidationStatus.NotEvaluated)]
    public async Task TrustFindingsStaySeparateFromSignatureIntegrity(ValidationStatus trustStatus)
    {
        var provider = new RecordingProvider { TrustStatus = trustStatus };
        var result = (
            await BiometricSignatureWorkflow.SignAsync(
                SignatureTestData.Record(),
                SignatureTestData.Identity(),
                provider,
                provider
            )
        ).Value;
        result.IsSuccess.Should().BeTrue();
        result.Verification.Value.SignatureFindings.Trust.Should().Be(trustStatus);
    }

    [Test]
    public async Task AuthenticatedIdentityMustMatchTheExpectedCard()
    {
        var provider = new RecordingProvider { WrongIdentity = true };
        var result = (
            await BiometricSignatureWorkflow.SignAsync(
                SignatureTestData.Record(),
                SignatureTestData.Identity(),
                provider,
                provider
            )
        ).Value;
        using (new AssertionScope())
        {
            result.Failure.Should().Be(SignatureFailure.IdentityMismatch);
            result.Record.HasNoValue.Should().BeTrue();
        }
    }

    [Test]
    public async Task SigningRejectsHeaderIdentityMismatchBeforeCallingProvider()
    {
        var provider = new RecordingProvider();
        var result = await BiometricSignatureWorkflow.SignAsync(
            SignatureTestData.Record(),
            SignatureTestData.IdentityWithFascN(9),
            provider,
            provider
        );
        using (new AssertionScope())
        {
            result.Should().Fail().Which.Code.Should().Be(PivErrorCodes.HeaderIdentityMismatch);
            provider.Requests.Should().BeEmpty();
        }
    }
}
