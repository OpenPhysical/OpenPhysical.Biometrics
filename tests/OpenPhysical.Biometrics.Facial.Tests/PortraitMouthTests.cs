// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitMouthTests
{
    [TestCase(4, true)]
    [TestCase(4.01, false)]
    public void PolicyChecksTheLargestInnerLipGapWithoutTreatingItAsAnIsoThreshold(
        double gap,
        bool closed
    )
    {
        var face = Mouth(gap);
        var result = PortraitMouth.Assess(face, PortraitMouthPolicy.Default);
        result.IsSuccess.Should().Be(closed);
        if (!closed)
        {
            result.Error.Code.Should().Be("MouthOpen");
            result.Error.TestName.Should().Be("Mouth closed");
            result.Error.Action.Should().Be(PortraitFindingAction.Retake);
            result.Error.CanOverride.Should().BeTrue();
        }
        var policy = PortraitMouthPolicy.Create(.05);
        policy.Should().Succeed();
        PortraitMouth.Assess(face, policy.Value).Should().Succeed();
    }

    [Test]
    public void MeasurementIsIndependentOfImageTranslationRotationAndUniformScale()
    {
        var face = Mouth(6);
        var radians = 37 * Math.PI / 180;
        var transformed = face with
        {
            Landmarks = face
                .Landmarks.Select(p => new FaceMeshPoint(
                    200 + .25 * (Math.Cos(radians) * p.X - Math.Sin(radians) * p.Y),
                    300 + .25 * (Math.Sin(radians) * p.X + Math.Cos(radians) * p.Y),
                    0
                ))
                .ToImmutableArray(),
        };
        var result = PortraitMouth.Measure(transformed);
        result.Should().Succeed();
        result.Value.MaximumOpeningRatio.Should().BeApproximately(.06, 1e-12);
    }

    [TestCase(0)]
    [TestCase(double.NaN)]
    [TestCase(1)]
    public void InvalidPolicyCannotSilentlyDisableTheCheck(double maximum)
    {
        PortraitMouthPolicy
            .Create(maximum)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidMouthPolicy);
    }

    [Test]
    public void MissingNonfiniteOrCollapsedMouthCannotPassAsClosed()
    {
        var face = Mouth(0);
        var invalid = new[]
        {
            face with
            {
                Landmarks = [],
            },
            face with
            {
                Landmarks = face.Landmarks.SetItem(13, new(double.NaN, 10, 0)),
            },
            face with
            {
                Landmarks = face.Landmarks.SetItem(308, face.Landmarks[78]),
            },
        };
        foreach (var source in invalid)
        {
            var result = PortraitMouth.Assess(source, PortraitMouthPolicy.Default);
            result.Should().Fail().Which.Code.Should().Be("MouthUnassessable");
            result.Error.Action.Should().Be(PortraitFindingAction.Retake);
            result.Error.CanOverride.Should().BeTrue();
        }
    }

    [TestCase("closed-mouth.jpg", true)]
    [TestCase("bush.jpg", false)]
    public void RealCachedLandmarksDistinguishClosedLipsFromVisibleTeeth(string name, bool closed)
    {
        using var service = TestFixtures.NewPreparer();
        var image = PortraitImage.Decode(TestFixtures.Bytes(name)).Value;
        var face = service.Analyze(image).Value.Faces.Single();
        var measurement = PortraitMouth.Measure(face);
        measurement.Should().Succeed();
        PortraitMouth.Assess(face, PortraitMouthPolicy.Default).IsSuccess.Should().Be(closed);
    }

    [Test]
    public void OpenMouthKeepsTheRealCroppedPreviewButRequiresAnExplicitOverride()
    {
        var (portrait, _) = TestFixtures.PreparedPortrait();
        portrait.RgbPixels.Length.Should().Be(PortraitOutput.Width * PortraitOutput.Height * 3);
        portrait.Status.Should().Be(PortraitStatus.ReviewRequired);
        portrait.Findings.Should().Contain(f => f.Code == "MouthOpen" && f.CanOverride);
        portrait.MouthPolicy.Should().Be(PortraitMouthPolicy.Default);
        portrait.MouthMeasurement.HasValue.Should().BeTrue();
        portrait.MouthMeasurement.Value.MaximumOpeningRatio.Should().BeInRange(.11, .13);
    }

    private static PortraitFace Mouth(double largestGap)
    {
        var points = Enumerable.Repeat(new FaceMeshPoint(50, 50, 0), 478).ToImmutableArray();
        points = points
            .SetItem(78, new(0, 0, 0))
            .SetItem(308, new(100, 0, 0))
            .SetItem(13, new(50, 0, 0))
            .SetItem(14, new(50, 0, 0))
            .SetItem(82, new(25, 0, 0))
            .SetItem(87, new(25, largestGap, 0))
            .SetItem(312, new(75, 0, 0))
            .SetItem(317, new(75, 0, 0));
        return new(0, 1, 1, points, default, default);
    }
}
