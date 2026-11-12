// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class UnsignedTemplateTests
{
    [Test]
    public void SingleViewPreservesTheDisplayedPointsAndAllowsUnreportedEquipment()
    {
        var capture = StorageFixtures.Capture();
        var result = UnsignedTemplateService.Create([capture], new TemplateMetadata(1, 0, 0));
        result.Should().Succeed();
        result.Value.Views[0].Should().BeSameAs(capture.View);
        result.Value.CaptureEquipment.Should().Be(0);
        result.Value.XResolution.Should().Be(197);
        result.Value.YResolution.Should().Be(197);
        Incits378Codec.Parse(Incits378Codec.Write(result.Value).Value).Should().Succeed();
    }

    [Test]
    public void TwoViewsKeepCaptureOrderAndUseTheLargestDimensions()
    {
        var first = StorageFixtures.Capture(FingerPosition.LeftIndex, 8, 9);
        var second = StorageFixtures.Capture(FingerPosition.RightIndex, 9, 8);
        var record = UnsignedTemplateService
            .Create([first, second], new TemplateMetadata(1, 2, 3))
            .Value;
        record.Width.Should().Be(9);
        record.Height.Should().Be(9);
        record.Views.Select(view => view.FingerPosition).Should().Equal(7, 2);
        record.CaptureEquipment.Should().Be(3);
    }

    [Test]
    public void InvalidMetadataAndDuplicateFingersAreRejected()
    {
        var capture = StorageFixtures.Capture();
        UnsignedTemplateService
            .Create([capture], new TemplateMetadata(0, 0, 0))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Template.Metadata");
        UnsignedTemplateService
            .Create([capture], new TemplateMetadata(1, 0, 4096))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Template.Metadata");
        UnsignedTemplateService
            .Create([capture, capture], new TemplateMetadata(1, 0, 0))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Template.Captures");
        UnsignedTemplateService
            .Create(ImmutableArray<CapturedFingerprint>.Empty, new TemplateMetadata(1, 0, 0))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Template.Captures");
    }

    [Test]
    public void DisplayedFingerQualityMustAgreeWithItsMeasuredNfiqAssessment()
    {
        var capture = StorageFixtures.Capture();
        var inconsistent = capture with { View = capture.View with { Quality = 20 } };
        UnsignedTemplateService
            .Create([inconsistent], new TemplateMetadata(1, 0, 0))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Template.Captures");
    }
}
