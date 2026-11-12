// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NBIS.Fixtures;

[TestFixture]
internal sealed class Sd04AlignmentTests
{
    [TestCase("f0005_03", "s0005_03")]
    [TestCase("f0002_05", "s0002_05")]
    public void PublicPairedImpressionsHaveOneToOneGeometricSupport(string first, string second)
    {
        var fixtures = new Sd04Fixtures(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "sd04")
        );
        var images = fixtures.Pairs.SelectMany(pair => new[] { pair.First, pair.Second }).ToArray();
        var reference = Extract(images.Single(image => image.Id == first));
        var probe = Extract(images.Single(image => image.Id == second));
        var result = FingerprintAlignment.Align(reference, 0, probe, 0);
        result.Should().Succeed();
        result.Value.Pairs.Should().HaveCountGreaterThanOrEqualTo(12);
        result
            .Value.Pairs.Select(pair => pair.ProbeIndex)
            .Distinct()
            .Should()
            .HaveCount(result.Value.Pairs.Length);
        result.Value.RootMeanSquareError.Should().BeLessThan(12);
    }

    [TestCase("0003_10")]
    [TestCase("0021_07")]
    [TestCase("0010_01")]
    public void PairedImpressionsWithoutUniqueSufficientGeometryFailClosed(string identifier)
    {
        var fixtures = new Sd04Fixtures(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "sd04")
        );
        var images = fixtures.Pairs.SelectMany(pair => new[] { pair.First, pair.Second }).ToArray();
        var reference = Extract(images.Single(image => image.Id == "f" + identifier));
        var probe = Extract(images.Single(image => image.Id == "s" + identifier));
        FingerprintAlignment
            .Align(reference, 0, probe, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AlignmentNotEstablished);
    }

    [TestCase("f0070_10")]
    [TestCase("f0003_10")]
    [TestCase("f0002_05")]
    [TestCase("f0021_07")]
    [TestCase("f0010_01")]
    public void DifferentPublicFingersHaveNoSupportedUniqueOverlay(string other)
    {
        var fixtures = new Sd04Fixtures(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "sd04")
        );
        var images = fixtures.Pairs.SelectMany(pair => new[] { pair.First, pair.Second }).ToArray();
        var reference = Extract(images.Single(image => image.Id == "f0005_03"));
        var probe = Extract(images.Single(image => image.Id == other));
        FingerprintAlignment
            .Align(reference, 0, probe, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AlignmentNotEstablished);
    }

    private static FingerprintRecord Extract(Sd04Image image)
    {
        var extracted = new NbisFingerprintEngine().Extract(image.Load());
        var points = extracted
            .Minutiae.Select(
                point => new OpenPhysical.Biometrics.Formats.Fingerprints.FingerprintMinutia(
                    (byte)point.Type,
                    (ushort)point.X,
                    (ushort)(extracted.Height - point.Y),
                    (byte)(((point.Angle + 180) % 360) / 2),
                    (byte)point.Quality
                )
            )
            .ToImmutableArray();
        return new(
            1,
            1,
            0,
            (ushort)extracted.Width,
            (ushort)extracted.Height,
            197,
            197,
            [new(3, 0, 0, 80, points, [])]
        );
    }
}
