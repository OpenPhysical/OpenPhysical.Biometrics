// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactSupportTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void FreshChangedSmearWithALocalRidgePatchRetainsADiagnosticWithoutAuthenticityGuarantee(
        bool repeatedDots
    )
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var observed = rig.Observe(
            repeatedDots
                ? HysteresisFingerFixture.WeakDots()
                : HysteresisFingerFixture.StrongSmear()
        );
        observed.Contact.Present.Should().BeTrue();
        var contactArea = observed.Contact.ContactMask.Count(value => value != 0);
        var observedArea = observed.Contact.Diagnostics.ObservedContactMask.Count(value =>
            value != 0
        );
        observedArea.Should().BeGreaterThan(80000);
        if (repeatedDots)
            observedArea.Should().BeGreaterThan(contactArea);
        else
            contactArea.Should().BeGreaterThan(80000);
        var record = observed.Proof.Record;
        record.Metrics.ContactArea.Should().Be(contactArea);
        record.ObservedMetrics.ContactArea.Should().Be(observedArea);
        observed
            .Proof.Qualified.Should()
            .BeTrue("compatible surface change is not a guarantee of fingerprint authenticity");
        observed.Proof.ExposureReady.Should().BeTrue();
        record.RidgeEvidence.LargestRidgeExteriorArea.Should().BeGreaterThanOrEqualTo(4000);
    }

    [Test]
    public void SupportedFaintPeripheralRidgesCanQualifyWithoutAuthorizingTheOutsideBackground()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var observed = rig.Observe(HysteresisFingerFixture.Annulus());
        observed.Proof.Qualified.Should().BeTrue();
        observed.Contact.Diagnostics.ContactMask[350 * 800 + 264].Should().Be(1);
        observed.Contact.Diagnostics.ContactMask[350 * 800 + 220].Should().Be(0);
    }
}
