// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ContactContourGuardTests
{
    [Test]
    public void ModifiedPixelGuardIsAppliedToContextCutRatherThanContact()
    {
        var fixture = new SupportGeometryFixture();
        _ = SupportGeometryFixture.Rectangle(fixture.Candidate, 300, 200, 80, 120);
        Array.Copy(fixture.Candidate, fixture.Support, fixture.Support.Length);
        var shape = Fs50ContactContourGeometryTests.Create(fixture).Value;
        var modified = shape.ToProcessingRegion(true).Value;
        modified.AppliedGuard.Should().Be(8);
        modified.Mask.Inner.Should().Equal(shape.FeatureMask);
        modified.GuardedContextMask[200 * 800 + 300].Should().Be(1);
        modified.Mask.Inner[200 * 800 + 300].Should().Be(1);
        modified.GuardedContextMask[180 * 800 + 280].Should().Be(1);
        modified.Mask.Inner[180 * 800 + 280].Should().Be(0);
    }

    [Test]
    public void NaturalImageEdgesAreNotArtificialCuts()
    {
        var contact = Enumerable.Repeat((byte)1, 800 * 750).ToArray();
        var shape = Fs50ContactContourGeometry
            .Create(contact, contact, Fs50ContactContourPolicy.Default, CancellationToken.None)
            .Value;
        var region = shape.ToProcessingRegion(true).Value;
        region.GuardedContextMask.Should().Equal(contact);
        region.Mask.Inner.Should().Equal(contact);
    }

    [Test]
    public void ContextMarginChangesCannotChangeContactHoleFeatureOrSupportPlanes()
    {
        var fixture = new SupportGeometryFixture();
        _ = SupportGeometryFixture.Rectangle(fixture.Candidate, 0, 0, 80, 100);
        Array.Copy(fixture.Candidate, fixture.Support, fixture.Support.Length);
        var policy = Fs50ContactContourPolicy.Create(new() { ContextMargin = 0 }).Value;
        var shape = Fs50ContactContourGeometry
            .Create(fixture.Candidate, fixture.Support, policy, CancellationToken.None)
            .Value;
        var region = shape.ToProcessingRegion(true).Value;
        region.Mask.Inner[20 * 800].Should().Be(1);
        region.Mask.Inner[20 * 800 + 79].Should().Be(0);
        region.Mask.Inner[20].Should().Be(1);
        region.Mask.Inner[99 * 800 + 20].Should().Be(0);
        var expanded = Fs50ContactContourGeometryTests.Create(fixture).Value;
        expanded.FeatureMask.Should().Equal(shape.FeatureMask);
        expanded.ContactMask.Should().Equal(shape.ContactMask);
        expanded.FilledHoleMask.Should().Equal(shape.FilledHoleMask);
        expanded.SupportMask.Should().Equal(shape.SupportMask);
    }
}
