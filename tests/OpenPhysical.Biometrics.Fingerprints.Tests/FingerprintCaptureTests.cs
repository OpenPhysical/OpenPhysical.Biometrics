// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintCaptureTests
{
    [Test]
    public void CaptureOwnsItsPixels()
    {
        var pixels = new byte[128 * 128];
        var capture = TestCaptures.Capture(FingerPosition.RightIndex, pixels);
        pixels[0] = 255;
        capture.Image.Pixels[0].Should().Be(0);
    }

    [TestCase(0, FingerprintImpression.PlainLiveScan, 3, 500, FingerprintErrorCodes.InvalidFinger)]
    [TestCase(
        2,
        FingerprintImpression.PlainLiveScan,
        0,
        500,
        FingerprintErrorCodes.InvalidEquipment
    )]
    [TestCase(
        2,
        FingerprintImpression.PlainLiveScan,
        4096,
        500,
        FingerprintErrorCodes.InvalidEquipment
    )]
    [TestCase(2, (FingerprintImpression)1, 3, 500, FingerprintErrorCodes.InvalidImpression)]
    [TestCase(2, FingerprintImpression.PlainLiveScan, 3, 300, FingerprintErrorCodes.InvalidCapture)]
    public void CaptureReportsInvalidMetadataAndGeometryAsFailures(
        int finger,
        FingerprintImpression impression,
        int equipment,
        int dotsPerInch,
        string code
    ) =>
        Create((FingerPosition)finger, impression, (ushort)equipment, dotsPerInch)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(code);

    [Test]
    public void CaptureRejectsPixelsThatDoNotMatchTheDimensions() =>
        FingerprintCapture
            .Create(
                new byte[10],
                128,
                128,
                500,
                FingerPosition.RightIndex,
                DateTimeOffset.UnixEpoch,
                FingerprintImpression.PlainLiveScan,
                3,
                true
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidCapture);

    private static Result<FingerprintCapture, FingerprintError> Create(
        FingerPosition finger,
        FingerprintImpression impression,
        ushort equipment,
        int dotsPerInch
    ) =>
        FingerprintCapture.Create(
            new byte[128 * 128],
            128,
            128,
            dotsPerInch,
            finger,
            DateTimeOffset.UnixEpoch,
            impression,
            equipment,
            true
        );
}
