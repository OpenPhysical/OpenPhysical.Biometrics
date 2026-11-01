// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class ProviderFailureTests
{
    [Test]
    public async Task ProviderExceptionsAndCancellationAreReportedWithStableCodes()
    {
        var failing = new RecordingProvider { ThrowOnSign = true };
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var healthy = new RecordingProvider();
        var failed = await BiometricSignatureWorkflow.SignAsync(
            SignatureTestData.Record(),
            SignatureTestData.Identity(),
            failing,
            failing
        );
        var cancelled = await BiometricSignatureWorkflow.SignAsync(
            SignatureTestData.Record(),
            SignatureTestData.Identity(),
            healthy,
            healthy,
            cancellation.Token
        );
        using (new AssertionScope())
        {
            failed.Should().Fail().Which.Code.Should().Be(PivErrorCodes.SigningProviderFailed);
            cancelled.Should().Fail().Which.Code.Should().Be(PivErrorCodes.Cancelled);
            healthy.Requests.Should().BeEmpty();
        }
    }

    [Test]
    public async Task VerifierExceptionsAreReportedWithAStableCode()
    {
        var provider = new RecordingProvider { ThrowOnVerify = true };
        var result = await BiometricSignatureWorkflow.SignAsync(
            SignatureTestData.Record(),
            SignatureTestData.Identity(),
            provider,
            provider
        );
        result.Should().Fail().Which.Code.Should().Be(PivErrorCodes.VerificationProviderFailed);
    }

    [Test]
    public async Task VerificationRequiresASignatureOfTheDeclaredLength()
    {
        var provider = new RecordingProvider();
        var result = await BiometricSignatureWorkflow.VerifyAsync(
            SignatureTestData.Record(),
            SignatureTestData.Identity(),
            provider
        );
        using (new AssertionScope())
        {
            result.Should().Fail().Which.Code.Should().Be(PivErrorCodes.SignatureLength);
            provider.Verifications.Should().Be(0);
        }
    }
}
