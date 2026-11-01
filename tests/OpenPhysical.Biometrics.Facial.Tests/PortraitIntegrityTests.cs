// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitIntegrityTests
{
    [Test]
    public void MissingModelResourceFailsWithAStableCode()
    {
        MediaPipeModelLoader
            .Load("absent.onnx", "00")
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.ModelMissing);
    }

    [Test]
    public void TamperedModelChecksumFailsWithAStableCode()
    {
        MediaPipeModelLoader
            .Load("face_detection_short_range.onnx", "00")
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.ModelChecksum);
    }

    [Test]
    public void CorrectModelChecksumIsAcceptedRegardlessOfCase()
    {
        MediaPipeModelLoader
            .Load(
                "face_detection_short_range.onnx",
                "2F2689B040BECF555706D2CB978D2F0E3296EA82413734FBA9A856C66C5F2B17"
            )
            .Should()
            .Succeed();
    }

    [Test]
    public void DisposedPreparerFailsInsteadOfThrowing()
    {
        var service = TestFixtures.NewPreparer();
        service.Dispose();
        service.Dispose();
        service
            .Prepare(TestFixtures.Bytes("glenn.jpg"))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.Disposed);
    }

    [Test]
    public void TransformWithoutAnInverseFailsToMapPoints()
    {
        new PortraitTransform(0, 0, 1, 1)
            .Inverse(new(1, 1))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.SingularTransform);
        new PortraitPixels(2, 2, new byte[12])
            .Warp(4, 4, new PortraitTransform(double.NaN, 0, 0, 0))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.SingularTransform);
    }

    [Test]
    public void TransformInverseRestoresMappedPoints()
    {
        var transform = new PortraitTransform(.8, .2, 12, -7);
        var point = new PortraitPoint(532, 731);
        var restored = transform.Inverse(transform.Map(point));
        restored.Should().Succeed();
        restored.Value.X.Should().BeApproximately(point.X, 1e-8);
        restored.Value.Y.Should().BeApproximately(point.Y, 1e-8);
    }
}
