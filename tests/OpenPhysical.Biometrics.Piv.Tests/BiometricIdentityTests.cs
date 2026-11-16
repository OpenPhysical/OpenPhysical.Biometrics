// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

/// <summary>Separates existing null-UUID references from ordinary issuance identity construction.</summary>
[TestFixture]
public sealed class BiometricIdentityTests
{
    /// <summary>Ordinary PIV issuance continues to reject the Legacy TWIC null identity.</summary>
    [Test]
    public void IssuanceFactoryRejectsNullUuid()
    {
        BiometricIdentity
            .Create(new byte[25], Guid.Empty)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PivErrorCodes.InvalidCardUuid);
    }

    /// <summary>The explicit existing-reference factory owns the original FASC-N without inventing a UUID.</summary>
    [Test]
    public void ExistingReferenceCanExplicitlyRepresentAbsentUuid()
    {
        var bytes = Enumerable.Range(0, 25).Select(value => (byte)value).ToArray();
        var result = BiometricIdentity.CreateWithoutCardUuid(bytes);
        result.Should().Succeed();
        result.Value.CardUuid.Should().Be(Guid.Empty);
        result.Value.FascN.Should().Equal(bytes);
        Array.Clear(bytes);
        result.Value.FascN[1].Should().Be(1);
        result
            .Value.Should()
            .NotBe(BiometricIdentity.Create(result.Value.FascN.AsSpan(), Guid.NewGuid()).Value);
    }

    /// <summary>Absent UUID permission does not relax the complete FASC-N requirement.</summary>
    /// <param name="length">Invalid encoded identity length.</param>
    [TestCase(0)]
    [TestCase(24)]
    [TestCase(26)]
    public void ExistingReferenceRejectsWrongIdentityLength(int length)
    {
        BiometricIdentity
            .CreateWithoutCardUuid(new byte[length])
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PivErrorCodes.InvalidFascN);
    }

    /// <summary>An existing-reference exception cannot enter the issuance signing workflow.</summary>
    /// <returns>Completion of the attempted signing operation.</returns>
    [Test]
    public async Task AbsentUuidCannotBePreparedOrSignedForIssuance()
    {
        var identity = BiometricIdentity.CreateWithoutCardUuid(new byte[25]).Value;
        var record = SignatureTestData.Record();
        BiometricSignatureWorkflow
            .Prepare(record, identity)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PivErrorCodes.InvalidCardUuid);
        var provider = new RecordingProvider();
        var signed = await BiometricSignatureWorkflow.SignAsync(
            record,
            identity,
            provider,
            provider
        );
        signed.Should().Fail().Which.Code.Should().Be(PivErrorCodes.InvalidCardUuid);
        provider.Requests.Should().BeEmpty();
        provider.Verifications.Should().Be(0);
    }
}
