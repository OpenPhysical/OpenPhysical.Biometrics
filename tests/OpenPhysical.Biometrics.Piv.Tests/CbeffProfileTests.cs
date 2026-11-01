// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;

[TestFixture]
internal sealed class CbeffProfileTests
{
    [Test]
    public void ProfileRejectsTheUnsupportedSignedSecurityOptionCombination()
    {
        var record = ProfileTestData.Wrapped(ProfileTestData.Fingerprints());
        var bytes = record.Header.ToArray();
        bytes[1] = 0x0F;
        record = record with { Header = CbeffHeader.FromBytes(bytes).Value };
        var findings = PivBiometricProfile.Validate(record, ProfileTestData.EvaluationTime);
        findings.Select(finding => finding.Code).Should().Contain("SignedPivSecurityOptions");
    }

    [Test]
    public void ValidityStartsAtOrAfterAcquisition()
    {
        var at = ProfileTestData.EvaluationTime;
        PivBiometricProfile
            .Validate(ProfileTestData.Wrapped(ProfileTestData.Fingerprints()), at)
            .Should()
            .BeEmpty();
        PivBiometricProfile
            .Validate(
                ProfileTestData.Wrapped(ProfileTestData.Fingerprints(), earlyValidity: true),
                at
            )
            .Select(finding => finding.Code)
            .Should()
            .Contain("ValidityStartsBeforeAcquisition");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CreatorHasAPrintableAsciiNameAndNullTerminator(bool missingTerminator)
    {
        var creator = Enumerable.Repeat((byte)'A', 18).ToArray();
        if (!missingTerminator)
        {
            creator[0] = 1;
            creator[17] = 0;
        }
        PivBiometricProfile
            .Validate(
                ProfileTestData.WrappedWithCreator(ProfileTestData.Fingerprints(), creator),
                ProfileTestData.EvaluationTime
            )
            .Select(finding => finding.Code)
            .Should()
            .Contain("CreatorEncoding");
    }

    [Test]
    public void AvailableFingerprintHeaderQualityUsesTheOriginalNfiqScale()
    {
        PivBiometricProfile
            .Validate(
                ProfileTestData.Wrapped(ProfileTestData.Fingerprints(), quality: 13),
                ProfileTestData.EvaluationTime
            )
            .Select(finding => finding.Code)
            .Should()
            .Contain("FingerprintHeaderQuality");
    }

    [Test]
    public void MalformedEnclosedRecordsAreReportedAgainstTheBiometricData()
    {
        var record = ProfileTestData.Wrapped(ProfileTestData.Fingerprints());
        var truncated = record with { BiometricData = record.BiometricData.RemoveAt(10) };
        PivBiometricProfile
            .Validate(truncated, ProfileTestData.EvaluationTime)
            .Where(finding => finding.Field == "BiometricData")
            .Should()
            .NotBeEmpty();
    }
}
