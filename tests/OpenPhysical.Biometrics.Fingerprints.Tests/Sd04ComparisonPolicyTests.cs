// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NBIS.Fixtures;

[TestFixture]
internal sealed class Sd04ComparisonPolicyTests
{
    private readonly IFingerprintEngine engine = new NbisFingerprintEngine();
    private readonly FingerprintMatchingPolicy fixturePolicy = new(
        "sd04-assortment-regression-v1",
        40
    );
    private FingerprintRecord reference = null!;
    private Sd04Fixtures fixtures = null!;

    [OneTimeSetUp]
    public void PrepareTwoFingerReferenceFromRealCaptures()
    {
        fixtures = new(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "sd04"));
        var enrollment = new PivFingerprintEnrollmentService(engine, new FixtureQuality());
        reference = enrollment.CreateRecord(
            enrollment.Prepare(Capture(Image("f0005_03"), FingerPosition.RightMiddle)),
            enrollment.Prepare(Capture(Image("f0003_10"), FingerPosition.LeftLittle)),
            new(1, 2, 3, 512, 512, equipmentAppendixFCertified: true)
        );
    }

    [TestCase("s0005_03", true)]
    [TestCase("f0070_10", false)]
    [TestCase("f0003_10", false)]
    public void ApplicationPolicySeparatesTheSelectedPairFromKnownNonmates(
        string imageId,
        bool accepted
    )
    {
        // The matrix records 125 for the paired impressions and 6/5 for these
        // nonmates. This named fixture policy exercises application decisions;
        // native tests own exact extraction and all ordered score baselines.
        // Capture metadata states the claimed reference finger. The nonmate
        // images come from other actual SD04 fingers, including the other view.
        var probeImage = Image(imageId).Load();
        var galleryImage = Image("f0005_03").Load();
        var probePixels = probeImage.Pixels.ToArray();
        var galleryPixels = galleryImage.Pixels.ToArray();
        var referenceBytes = Incits378Codec.Write(reference);
        var probeTemplate = engine.Extract(probeImage);
        var galleryTemplate = engine.Extract(galleryImage);
        var probePoints = probeTemplate.Minutiae.ToArray();
        var galleryPoints = galleryTemplate.Minutiae.ToArray();
        var scores = new[]
        {
            engine.Match(probeImage, galleryImage),
            engine.Match(probeImage, galleryTemplate),
            engine.Match(probeTemplate, galleryImage),
            engine.Match(probeTemplate, galleryTemplate),
        };
        var selectedReference = new FingerprintComparisonService(engine).Compare(
            Capture(Image(imageId), FingerPosition.RightMiddle),
            reference,
            FingerPosition.RightMiddle,
            0,
            fixturePolicy
        );
        var decisions = scores.Select(fixturePolicy.Evaluate).Append(selectedReference).ToArray();
        decisions.All(result => result.Accepted == accepted).Should().BeTrue();
        decisions
            .All(result => result.PolicyIdentifier == fixturePolicy.Identifier)
            .Should()
            .BeTrue();
        using (new AssertionScope())
        {
            probeImage.Pixels.ToArray().Should().Equal(probePixels);
            galleryImage.Pixels.ToArray().Should().Equal(galleryPixels);
            probeTemplate.Minutiae.Should().Equal(probePoints);
            galleryTemplate.Minutiae.Should().Equal(galleryPoints);
            Incits378Codec.Write(reference).Should().Equal(referenceBytes);
        }
    }

    [Test]
    public void EvenDegreeReferencePointsPreserveGeometryAndMatchScoreThroughCanonicalConversion()
    {
        var source = engine.Extract(Image("f0005_03").Load());
        // Even-degree directions map exactly to the two-degree interchange grid.
        // Restricting this fixture subset makes round-trip equality meaningful.
        var points = source.Minutiae.Where(point => point.Angle % 2 == 0).ToArray();
        var subset = new FingerprintTemplate(
            source.Width,
            source.Height,
            points,
            "SD04 exact-angle subset"
        );
        var standard = points
            .Select(point => new OpenPhysical.Biometrics.Formats.Fingerprints.FingerprintMinutia(
                (byte)point.Type,
                (ushort)point.X,
                (ushort)(source.Height - point.Y),
                (byte)(((point.Angle + 180) % 360) / 2),
                (byte)point.Quality
            ))
            .ToImmutableArray();
        var record = new FingerprintRecord(
            1,
            1,
            0x8001,
            (ushort)source.Width,
            (ushort)source.Height,
            197,
            197,
            [new(3, 0, 0, 60, standard, [])]
        );
        var converted = record.ToMatchingTemplate(FingerPosition.RightMiddle, 0);
        converted.Minutiae.Should().Equal(subset.Minutiae);
        var gallery = engine.Extract(Image("s0005_03").Load());
        engine.Match(converted, gallery).Should().Be(engine.Match(subset, gallery));
    }

    [TestCase(39, false)]
    [TestCase(40, true)]
    [TestCase(41, true)]
    public void FixturePolicyAppliesItsInclusiveBoundary(int score, bool accepted)
    {
        var result = fixturePolicy.Evaluate(score);
        result.RawScore.Should().Be(score);
        result.Accepted.Should().Be(accepted);
    }

    [Test]
    public void PolicyRejectsInvalidNegativeScores() =>
        FluentActions
            .Invoking(() => fixturePolicy.Evaluate(-1))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();

    private Sd04Image Image(string identifier) =>
        fixtures
            .Pairs.SelectMany(pair => new[] { pair.First, pair.Second })
            .Single(image => image.Id == identifier);

    private static FingerprintCapture Capture(Sd04Image image, FingerPosition finger)
    {
        var capture = image.Load();
        return new(
            capture.Pixels,
            capture.Width,
            capture.Height,
            capture.DotsPerInch,
            finger,
            DateTimeOffset.Parse("2026-10-31T12:00:00Z"),
            FingerprintImpression.PlainLiveScan,
            3,
            equipmentAppendixFCertified: true
        );
    }

    private sealed class FixtureQuality : IFingerprintQualityProvider
    {
        public FingerprintQualityAssessment Assess(FingerprintCapture capture) =>
            new(2, "sd04-workflow-fixture");
    }
}
