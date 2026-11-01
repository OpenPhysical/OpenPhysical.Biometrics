// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;

internal sealed class SignatureRequestTests
{
    [Test]
    public void RequestsAndIdentityOwnTheirBuffersAndDeclareCorrectProfileOids()
    {
        var identityBytes = new byte[25];
        var identity = BiometricIdentity.Create(identityBytes, SignatureTestData.CardUuid).Value;
        var bytes = CbeffCodec.GetSignedData(SignatureTestData.Record()).Value;
        var request = BiometricSignatureRequest.Create(bytes, identity).Value;
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
    public void IdentitiesRequireAFascNAndACardUuidAndCompareByValue()
    {
        using (new AssertionScope())
        {
            BiometricIdentity
                .Create(new byte[24], SignatureTestData.CardUuid)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(PivErrorCodes.InvalidFascN);
            BiometricIdentity
                .Create(new byte[25], Guid.Empty)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(PivErrorCodes.InvalidCardUuid);
            SignatureTestData.Identity().Should().Be(SignatureTestData.Identity());
            SignatureTestData.Identity().Should().NotBe(SignatureTestData.IdentityWithFascN(1));
        }
    }

    [Test]
    public void SignedContentRequiresTheCompleteCanonicalHeader()
    {
        BiometricSignatureRequest
            .Create(new byte[CbeffHeader.Size - 1], SignatureTestData.Identity())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PivErrorCodes.SignedContentLength);
        var request = BiometricSignatureRequest
            .Create(new byte[CbeffHeader.Size], SignatureTestData.Identity())
            .Value;
        request.SignedContent.Length.Should().Be(CbeffHeader.Size);
    }

    [Test]
    public void AttachmentRejectsLengthChangesAfterSigning()
    {
        using (new AssertionScope())
        {
            BiometricSignatureWorkflow
                .Attach(SignatureTestData.Record(), new byte[4])
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(PivErrorCodes.SignatureLength);
            BiometricSignatureWorkflow.Attach(SignatureTestData.Record(), []).Should().Fail();
        }
    }

    [Test]
    public void PreparationRequiresTheHeaderAndExpectedIdentityToAgree()
    {
        using (new AssertionScope())
        {
            BiometricSignatureWorkflow
                .Prepare(SignatureTestData.Record(), SignatureTestData.IdentityWithFascN(9))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(PivErrorCodes.HeaderIdentityMismatch);
            BiometricSignatureWorkflow
                .Prepare(SignatureTestData.Record(), SignatureTestData.Identity())
                .Should()
                .Succeed();
        }
    }
}
