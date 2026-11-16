// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.NBIS;
using MatchingMinutia = OpenPhysical.Biometrics.NBIS.FingerprintMinutia;
using StandardMinutia = OpenPhysical.Biometrics.Formats.Fingerprints.FingerprintMinutia;

[TestFixture]
internal sealed class FingerprintGeometrySourceTests
{
    // NIST xytreps.c computes these NIST/M1 angle pairs independently from LFS
    // directions 0, 8, 16, 24, 1 and 31. Rows remain unchanged in the M1 output.
    [TestCase(0, 0, 270, 45)]
    [TestCase(127, 127, 180, 0)]
    [TestCase(0, 127, 90, 135)]
    [TestCase(127, 0, 0, 90)]
    [TestCase(20, 28, 259, 39)]
    [TestCase(20, 28, 281, 50)]
    public void CanonicalConversionMatchesNistSourceAtCardinalDirectionsAndImageEdges(
        int x,
        int row,
        int nistAngle,
        byte m1Angle
    )
    {
        var source = new FingerprintTemplate(
            128,
            128,
            [new MatchingMinutia(x, 128 - row, nistAngle, 90, 1)],
            "NIST xytreps.c coordinate conventions"
        );
        var view = FingerprintViewFactory
            .Create(
                source,
                FingerprintQualityAssessment.Create(1, "test").Value,
                FingerPosition.RightIndex,
                0,
                FingerprintImpression.PlainLiveScan
            )
            .Value;
        view.Minutiae.Single()
            .Should()
            .Be(new StandardMinutia(1, (ushort)x, (ushort)row, m1Angle, 90));
    }
}
