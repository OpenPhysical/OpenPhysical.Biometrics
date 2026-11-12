// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactObservationIntegrityTests
{
    [Test]
    public void RepeatingOnePhysicalEmptyFrameDoesNotSatisfyTwoFrameRemoval()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        rig.Observe(pixels).Proof.Qualified.Should().BeTrue();
        var empty = rig.Observe(Fs50EvidenceFrameFixture.Empty());
        var repeated = rig.Learning.Observe(empty.Frame, empty.Contact, "session").Value;
        rig.Qualification.Observe(
                empty.Frame,
                empty.Contact,
                repeated,
                "session",
                CancellationToken.None
            )
            .Should()
            .Succeed();
        rig.Observe(pixels, 220).Proof.Qualified.Should().BeTrue();
    }

    [Test]
    public void ReusingASequenceForDifferentOriginalPixelsIsATypedFailure()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var first = rig.Observe(pixels);
        pixels[0] ^= 1;
        var wrong = Fs50EvidenceFrameFixture.Create(pixels, first.Frame.Sequence);
        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;
        var sensor = rig.Learning.Observe(wrong, contact, "session").Value;
        rig.Qualification.Observe(wrong, contact, sensor, "session", CancellationToken.None)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidFrame);
    }

    [Test]
    public void DisjointCorroboratedAndUncorroboratedAreasCannotExceedContact()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var observed = rig.Observe(ContactEvidenceFixtures.PublicFinger("f0005_03.gray8"));
        var contour = Fs50ContactContourRegion
            .Create(
                observed.Frame,
                observed.Contact.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        var record = observed.Proof.Record;
        var impossible = record with
        {
            Metrics = record.Metrics with
            {
                CorroboratedArea = record.Metrics.ContactArea,
                UncorroboratedExteriorArea = 1,
                StaticExteriorArea = 0,
                UnobservedExteriorArea = 1,
                LargestUncorroboratedExteriorArea = 1,
            },
        };
        impossible.Validate(contour).Should().Fail();
    }
}
