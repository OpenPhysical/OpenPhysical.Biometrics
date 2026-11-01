// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintComparisonTests
{
    [Test]
    public void ComparisonRecordsTheExplicitPolicyAndFingerSelection()
    {
        var engine = new TestEngine();
        var service = new PivFingerprintEnrollmentService(engine, new TestQuality());
        var probe = TestCaptures.Capture(FingerPosition.RightIndex);
        var record = service
            .CreateRecord(
                service.Prepare(probe).Value,
                service.Prepare(TestCaptures.Capture(FingerPosition.LeftIndex)).Value,
                TestCaptures.Metadata()
            )
            .Value;
        var policy = FingerprintMatchingPolicy.Create("site-policy-v3", 43).Value;
        var comparison = new FingerprintComparisonService(engine).Compare(
            probe,
            record,
            FingerPosition.RightIndex,
            0,
            policy
        );
        var mismatch = new FingerprintComparisonService(engine).Compare(
            probe,
            record,
            FingerPosition.LeftIndex,
            0,
            policy
        );
        using (new AssertionScope())
        {
            comparison
                .Value.Should()
                .Be(new FingerprintComparisonResult(42, false, "site-policy-v3"));
            mismatch.Should().Fail().Which.Code.Should().Be(FingerprintErrorCodes.FingerMismatch);
        }
    }

    [Test]
    public void PoliciesRequireAnIdentifierAndANonnegativeMinimum()
    {
        using (new AssertionScope())
        {
            FingerprintMatchingPolicy
                .Create(" ", 40)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.InvalidPolicy);
            FingerprintMatchingPolicy
                .Create("policy", -1)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.InvalidScore);
        }
    }
}
