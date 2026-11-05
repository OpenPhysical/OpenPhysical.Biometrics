// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using Org.BouncyCastle.Security;
using static OpenPhysical.Biometrics.Fingerprints.Tests.AcquisitionFixtures;

[TestFixture]
internal sealed class FingerprintPreparationEvidenceTests
{
    [Test]
    public void RecordRetainsExactAcquisitionAndProcessingLineageWithoutReextracting()
    {
        var engine = new TestEngine();
        var service = Service(engine);
        var frame = Frame();
        var first = service.Prepare(frame, FingerPosition.RightIndex, Profile()).Value;
        var second = service.Prepare(Frame(), FingerPosition.LeftIndex, Profile()).Value;
        var prepared = service.CreateRecord(first, second, TestCaptures.Metadata()).Value;
        var parsed = Incits378Codec.Parse(prepared.Incits378.AsSpan()).Value.Record;
        Incits378Codec.Write(parsed).Value.Should().Equal(prepared.Incits378);
        engine.Extractions.Should().Be(2);
        prepared
            .Evidence.Select(e => e.Finger)
            .Should()
            .Equal(FingerPosition.RightIndex, FingerPosition.LeftIndex);
        var evidence = prepared.Evidence[0];
        evidence.AcquisitionProfile.Value.Id.Should().Be("profile");
        evidence.Provenance.Value.Should().BeSameAs(frame.Provenance);
        evidence.CapturedAt.Value.Should().Be(frame.CapturedAt);
        evidence.FrameSequence.Value.Should().Be(frame.Sequence);
        evidence
            .CorrectedPixelSha256.Value.Should()
            .Be("4fe7b59af6de3b665b67788cc2f99892ab827efae3a467342b3bb4e3bc8e5bfe");
        evidence.Quality.Value.AlgorithmVersion.Should().Be("test-provider");
        evidence.Quality.Value.Nfiq1.Should().Be(2);
        evidence.ExtractorProvenance.Value.Should().Be("test-engine");
        evidence.ProcessingVersion.Should().Be("OpenPhysical.PivEnrollmentRecordBuilder/v1");
        evidence.RecordSha256.Should().HaveLength(64);
        prepared.Evidence[1].RecordSha256.Should().Be(evidence.RecordSha256);
    }

    [Test]
    public void EvidenceDigestMatchesTheExactEncodedBytes()
    {
        var service = Service(new TestEngine());
        var first = service.Prepare(Frame(), FingerPosition.RightIndex, Profile()).Value;
        var second = service.Prepare(Frame(), FingerPosition.LeftIndex, Profile()).Value;
        var result = service.CreateRecord(first, second, TestCaptures.Metadata()).Value;
        var expected = Convert.ToHexStringLower(
            DigestUtilities.CalculateDigest("SHA-256", result.Incits378.ToArray())
        );
        result.Evidence.Should().OnlyContain(evidence => evidence.RecordSha256 == expected);
    }

    [Test]
    public void ChangingEitherViewChangesTheWholeRecordBinding()
    {
        var service = Service(new TestEngine());
        var primary = service.Prepare(Frame(), FingerPosition.RightIndex, Profile()).Value;
        var secondary = service.Prepare(Frame(), FingerPosition.LeftIndex, Profile()).Value;
        var changed = service.Prepare(Frame(), FingerPosition.LeftMiddle, Profile()).Value;
        var original = service.CreateRecord(primary, secondary, TestCaptures.Metadata()).Value;
        var replacement = service.CreateRecord(primary, changed, TestCaptures.Metadata()).Value;
        replacement.Incits378.Should().NotEqual(original.Incits378);
        replacement.Evidence[0].RecordSha256.Should().NotBe(original.Evidence[0].RecordSha256);
        replacement.Evidence[1].RecordSha256.Should().Be(replacement.Evidence[0].RecordSha256);
    }

    [Test]
    public void UnavailableViewsHaveNoInventedCaptureOrQuality()
    {
        var service = Service(new TestEngine());
        var primary = PreparedAcquiredFingerprint
            .Unavailable(FingerPosition.RightIndex, FingerprintAvailability.TemporarilyUnusable)
            .Value;
        var secondary = PreparedAcquiredFingerprint
            .Unavailable(FingerPosition.LeftIndex, FingerprintAvailability.PermanentlyUnusable)
            .Value;
        var result = service.CreateRecord(primary, secondary, TestCaptures.Metadata()).Value;
        result
            .Evidence.Select(e => e.Availability)
            .Should()
            .Equal(
                FingerprintAvailability.TemporarilyUnusable,
                FingerprintAvailability.PermanentlyUnusable
            );
        foreach (var evidence in result.Evidence)
        {
            evidence.AcquisitionProfile.HasNoValue.Should().BeTrue();
            evidence.Provenance.HasNoValue.Should().BeTrue();
            evidence.CapturedAt.HasNoValue.Should().BeTrue();
            evidence.FrameSequence.HasNoValue.Should().BeTrue();
            evidence.CorrectedPixelSha256.HasNoValue.Should().BeTrue();
            evidence.Quality.HasNoValue.Should().BeTrue();
            evidence.ExtractorProvenance.HasNoValue.Should().BeTrue();
        }
        Incits378Codec
            .Parse(result.Incits378.AsSpan())
            .Value.Record.Views.Select(v => v.Quality)
            .Should()
            .Equal(new byte[] { 255, 254 });
    }

    [Test]
    public void InvalidAcquisitionCannotReachExtraction()
    {
        var engine = new TestEngine();
        Service(engine)
            .Prepare(Frame(qualified: false), FingerPosition.RightIndex, Profile())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AcquisitionProfileMismatch);
        engine.Extractions.Should().Be(0);
    }

    [Test]
    public void ExistingRecordAndAvailabilityRulesRemainEnforced()
    {
        var service = Service(new TestEngine());
        var finger = service.Prepare(Frame(), FingerPosition.RightIndex, Profile()).Value;
        service
            .CreateRecord(finger, finger, TestCaptures.Metadata())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.DuplicateFingers);
        PreparedAcquiredFingerprint
            .Unavailable(FingerPosition.LeftIndex, FingerprintAvailability.Available)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidAvailability);
    }

    private static AcquiredFingerprintPreparationService Service(TestEngine engine) =>
        new(
            new AcquiredFingerprintAdapter(),
            new PivFingerprintEnrollmentService(engine, new TestQuality())
        );
}
