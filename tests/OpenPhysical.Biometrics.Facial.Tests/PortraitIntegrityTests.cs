// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitIntegrityTests
{
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
        new PortraitTransform(double.NaN, 0, 0, 0)
            .Inverse(new(1, 1))
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
