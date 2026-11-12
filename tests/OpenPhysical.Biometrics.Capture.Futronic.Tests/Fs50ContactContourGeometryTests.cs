// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ContactContourGeometryTests
{
    [Test]
    public void EnclosedWeakCoreIsFilledAsUncertaintyWithoutPruningAnyContact()
    {
        var fixture = new SupportGeometryFixture();
        _ = SupportGeometryFixture.Rectangle(fixture.Candidate, 300, 200, 160, 200);
        _ = SupportGeometryFixture.Rectangle(fixture.Candidate, 310, 210, 140, 180, 0);
        Array.Copy(fixture.Candidate, fixture.Support, fixture.Support.Length);
        var shape = Create(fixture).Value;
        shape.ContactMask.Should().Equal(fixture.Candidate);
        shape.SupportMask.Should().Equal(fixture.Support);
        shape.Statistics.SelectedContactArea.Should().Be(6800);
        shape.Statistics.FilledHoleArea.Should().Be(25200);
        shape.Statistics.FeatureArea.Should().Be(32000);
        shape.FilledHoleMask[300 * 800 + 380].Should().Be(1);
        shape.FeatureMask[300 * 800 + 380].Should().Be(1);
        shape.UncertainMask[300 * 800 + 380].Should().Be(1);
        var region = shape.ToProcessingRegion(false).Value;
        region.Mask.Inner.Should().Equal(shape.FeatureMask);
        region.Mask.Retained.Should().Equal(shape.ContextMask);
    }

    [Test]
    public void OpenNotchAndContextMarginNeverAuthorizeFeatures()
    {
        var fixture = new SupportGeometryFixture();
        _ = SupportGeometryFixture.Rectangle(fixture.Candidate, 300, 200, 160, 220);
        _ = SupportGeometryFixture.Rectangle(fixture.Candidate, 320, 200, 120, 200, 0);
        _ = SupportGeometryFixture.Rectangle(fixture.Support, 300, 400, 160, 20);
        var shape = Create(fixture).Value;
        shape.FeatureMask.Should().Equal(fixture.Candidate);
        shape.Statistics.FilledHoleArea.Should().Be(0);
        shape.ContextMask[250 * 800 + 380].Should().Be(1);
        shape.FeatureMask[250 * 800 + 380].Should().Be(0);
        shape.ContextMask[190 * 800 + 290].Should().Be(1);
        shape.FeatureMask[190 * 800 + 290].Should().Be(0);
        shape.FeatureMask[250 * 800 + 300].Should().Be(1);
        shape.SupportMask[250 * 800 + 300].Should().Be(0);
    }

    [Test]
    public void SparseRidgeEvidenceDoesNotEraseConnectedPeripheralContact()
    {
        var fixture = new SupportGeometryFixture();
        _ = SupportGeometryFixture.Rectangle(fixture.Candidate, 250, 200, 300, 320);
        _ = SupportGeometryFixture.Rectangle(fixture.Support, 350, 300, 64, 96);
        var shape = Create(fixture).Value;
        shape.FeatureMask.Should().Equal(fixture.Candidate);
        shape.FeatureMask[300 * 800 + 260].Should().Be(1);
        shape.SupportMask[300 * 800 + 260].Should().Be(0);
        shape.UncertainMask[300 * 800 + 260].Should().Be(1);
        shape.Statistics.MeasuredSupportArea.Should().Be(6144);
    }

    [Test]
    public void DiagonalContactUsesEightConnectivityAndFourConnectedExterior()
    {
        var fixture = new SupportGeometryFixture();
        foreach (var (x, y) in new[] { (1, 0), (0, 1), (2, 1), (1, 2) })
            fixture.Candidate[(300 + y) * 800 + 350 + x] = 1;
        Array.Copy(fixture.Candidate, fixture.Support, fixture.Support.Length);
        var shape = Create(fixture).Value;
        shape.Statistics.SelectedContactArea.Should().Be(4);
        shape.Statistics.FilledHoleArea.Should().Be(1);
        shape.FeatureMask[301 * 800 + 351].Should().Be(1);
    }

    [Test]
    public void GeometryRejectsDisconnectedOrNonbinaryContactAndSupportOutsideContact()
    {
        var fixture = new SupportGeometryFixture();
        fixture.Candidate[100] = fixture.Support[100] = 1;
        fixture.Candidate[1000] = 1;
        Create(fixture).IsFailure.Should().BeTrue();
        fixture.Candidate[1000] = 0;
        fixture.Support[1000] = 1;
        Create(fixture).Error.Code.Should().Be(CaptureErrorCodes.InvalidArgument);
        fixture.Support[1000] = 0;
        fixture.Candidate[100] = 2;
        Create(fixture).Error.Code.Should().Be(CaptureErrorCodes.InvalidArgument);
    }

    internal static CSharpFunctionalExtensions.Result<Fs50ContactContourShape, CaptureError> Create(
        SupportGeometryFixture fixture
    ) =>
        Fs50ContactContourGeometry.Create(
            fixture.Candidate,
            fixture.Support,
            Fs50ContactContourPolicy.Default,
            CancellationToken.None
        );
}
