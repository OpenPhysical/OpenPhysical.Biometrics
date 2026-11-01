// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class PivRecordCreationTests
{
    [Test]
    public void IssuanceRequiresTheSuppliedEquipmentCertification()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var first = service.Prepare(TestCaptures.Capture(FingerPosition.RightIndex)).Value;
        var second = service.Prepare(TestCaptures.Capture(FingerPosition.LeftIndex)).Value;
        var uncertified = PivFingerprintRecordMetadata
            .Create(1, 2, 3, 128, 128, equipmentAppendixFCertified: false)
            .Value;
        service
            .CreateRecord(first, second, uncertified)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.EquipmentCertification);
    }

    [Test]
    public void IssuanceSelectsHighestQualityAndPreservesSourceCoordinates()
    {
        var engine = new TestEngine();
        var service = new PivFingerprintEnrollmentService(engine, new TestQuality());
        var first = service.Prepare(TestCaptures.Capture(FingerPosition.RightIndex)).Value;
        var second = service.Prepare(TestCaptures.Capture(FingerPosition.LeftIndex)).Value;
        var record = service.CreateRecord(first, second, TestCaptures.Metadata()).Value;
        var encoded = Incits378Codec.Write(record).Value;
        var decoded = Incits378Codec.Parse(encoded).Value.Record;
        using (new AssertionScope())
        {
            decoded.Views.Should().HaveCount(2);
            decoded.Views[0].Minutiae.Should().HaveCount(128);
            decoded.Views[0].Minutiae[0].Quality.Should().Be(100);
            decoded.Views[0].Minutiae[0].Y.Should().Be(27);
            decoded.Views[0].Minutiae[0].Angle.Should().Be(90);
            decoded.Views[0].Quality.Should().Be(80);
            decoded.Views[0].ViewNumber.Should().Be(0);
            decoded.CaptureEquipment.Should().Be(0x8003);
            engine.Extractions.Should().Be(2);
        }
    }

    [Test]
    public void AvailableSecondaryRequiresExplicitPrimarySelection()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var available = service.Prepare(TestCaptures.Capture(FingerPosition.LeftIndex)).Value;
        var unavailable = FingerprintEnrollmentFinger
            .Unavailable(FingerPosition.RightIndex, FingerprintAvailability.TemporarilyUnusable)
            .Value;
        var metadata = TestCaptures.Metadata();
        var ordered = service.CreateRecord(available, unavailable, metadata).Value;
        using (new AssertionScope())
        {
            service
                .CreateRecord(unavailable, available, metadata)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.PrimaryMustBeAvailable);
            ordered.Views.Select(view => view.FingerPosition).Should().Equal(new byte[] { 7, 2 });
            ordered.Views.Select(view => view.Quality).Should().Equal(new byte[] { 80, 255 });
        }
    }

    [Test]
    public void UnavailableFingersCarryTheirDistinctProfileStates()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var record = service
            .CreateRecord(
                FingerprintEnrollmentFinger
                    .Unavailable(
                        FingerPosition.RightIndex,
                        FingerprintAvailability.TemporarilyUnusable
                    )
                    .Value,
                FingerprintEnrollmentFinger
                    .Unavailable(
                        FingerPosition.LeftIndex,
                        FingerprintAvailability.PermanentlyUnusable
                    )
                    .Value,
                TestCaptures.Metadata()
            )
            .Value;
        using (new AssertionScope())
        {
            record.Views.Select(view => view.Quality).Should().Equal(new byte[] { 255, 254 });
            record.Views.All(view => view.Minutiae.IsEmpty).Should().BeTrue();
            FingerprintRecordAdapter
                .ToMatchingTemplate(record, FingerPosition.LeftIndex, 0)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.FingerUnavailable);
        }
    }

    [Test]
    public void RecordCreationRejectsRepeatedFingersAndInconsistentMetadata()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var first = service.Prepare(TestCaptures.Capture(FingerPosition.RightIndex)).Value;
        var second = service.Prepare(TestCaptures.Capture(FingerPosition.LeftIndex)).Value;
        var wrongSize = PivFingerprintRecordMetadata
            .Create(1, 2, 3, 100, 128, equipmentAppendixFCertified: true)
            .Value;
        var wrongEquipment = PivFingerprintRecordMetadata
            .Create(1, 2, 4, 128, 128, equipmentAppendixFCertified: true)
            .Value;
        using (new AssertionScope())
        {
            service
                .CreateRecord(first, first, TestCaptures.Metadata())
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.DuplicateFingers);
            service
                .CreateRecord(first, second, wrongSize)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.RecordDimensions);
            service
                .CreateRecord(first, second, wrongEquipment)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.EquipmentMismatch);
        }
    }

    [Test]
    public void MetadataRejectsUnregisteredIdentifiersAndOutOfRangeGeometry()
    {
        using (new AssertionScope())
        {
            PivFingerprintRecordMetadata.Create(0, 2, 3, 128, 128, true).Should().Fail();
            PivFingerprintRecordMetadata.Create(1, 0, 3, 128, 128, true).Should().Fail();
            PivFingerprintRecordMetadata.Create(1, 2, 4096, 128, 128, true).Should().Fail();
            PivFingerprintRecordMetadata.Create(1, 2, 3, 0, 128, true).Should().Fail();
            PivFingerprintRecordMetadata.Create(1, 2, 3, 128, 2049, true).Should().Fail();
        }
    }
}
