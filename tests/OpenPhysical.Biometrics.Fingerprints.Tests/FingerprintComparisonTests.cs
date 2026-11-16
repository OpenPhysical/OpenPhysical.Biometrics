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
    public void CanonicalPreparedComparisonsReuseTheExactExportedSubset()
    {
        var engine = new TestEngine();
        var canonical = Canonical(engine);
        var policy = FingerprintMatchingPolicy.Create("test", 40).Value;
        var comparison = new FingerprintComparisonService(engine);
        for (var attempt = 0; attempt < 3; attempt++)
            comparison
                .Compare(
                    canonical,
                    FingerPosition.RightIndex,
                    canonical,
                    FingerPosition.RightIndex,
                    policy
                )
                .Value.Accepted.Should()
                .BeTrue();
        engine.Extractions.Should().Be(1);
        engine.Comparisons.Should().Be(3);
        engine.ComparedProbe.Value.Should().BeSameAs(canonical);
        engine.ComparedProbe.Value.Minutiae.Should().HaveCount(128);
    }

    [TestCase(0, FingerprintErrorCodes.InvalidFinger)]
    [TestCase(7, FingerprintErrorCodes.FingerMismatch)]
    public void PreparedComparisonRejectsInvalidOrDifferentFingersBeforeMatching(
        int finger,
        string code
    )
    {
        var engine = new TestEngine();
        var canonical = Canonical(engine);
        new FingerprintComparisonService(engine)
            .Compare(
                canonical,
                FingerPosition.RightIndex,
                canonical,
                (FingerPosition)finger,
                FingerprintMatchingPolicy.Create("test", 40).Value
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(code);
        engine.Comparisons.Should().Be(0);
    }

    private static NBIS.FingerprintTemplate Canonical(TestEngine engine)
    {
        var processing = new FingerprintPreparationService(engine, new TestQuality());
        var prepared = processing
            .Prepare(TestCaptures.Capture(FingerPosition.RightIndex).Image)
            .Value;
        var view = FingerprintViewFactory
            .Create(
                prepared.Template,
                prepared.Quality,
                FingerPosition.RightIndex,
                0,
                FingerprintImpression.PlainLiveScan
            )
            .Value;
        var record = new Formats.Fingerprints.FingerprintRecord(
            0,
            0,
            0,
            128,
            128,
            197,
            197,
            [view]
        );
        return record.ToMatchingTemplate(FingerPosition.RightIndex, 0).Value;
    }

    [Test]
    public void ComparisonRecordsTheExplicitPolicyAndFingerSelection()
    {
        var engine = new TestEngine();
        var service = new PivFingerprintEnrollmentService(
            new FingerprintPreparationService(engine, new TestQuality())
        );
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
