// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class CameraContractTests
{
    [Test]
    public void PreferredModeUsesAnAdvertisedRgbModeWithoutInventingGeometry()
    {
        var small = new CameraMode(640, 480, 30, 1, "RGB24");
        var large = new CameraMode(3840, 2160, 30, 1, "RGB32");
        var expected = new CameraMode(1920, 1080, 30, 1, "RGB32");
        var source = new CameraSource("test", "Test camera", [small, large, expected]);
        source.PreferredMode.Should().Succeed().Which.Should().BeSameAs(expected);
        new CameraSource("test", "YUV only", ImmutableArray<CameraMode>.Empty)
            .PreferredMode.Should()
            .Fail()
            .Which.Code.Should()
            .Be(CameraErrorCodes.UnsupportedMode);
    }
}
