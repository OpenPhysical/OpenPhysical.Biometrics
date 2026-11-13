// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PortraitBackgroundEdgeTests
{
    [TestCase(1e-10, true)]
    [TestCase(.01, false)]
    public void MaskSamplingToleratesOnlyRoundoffAtSourceSupportedCropEdges(
        double excess,
        bool supported
    )
    {
        using var service = TestFixtures.NewPreparer();
        var analysis = service
            .PrepareBackground(
                service.Analyze(PortraitImage.Decode(TestFixtures.Bytes("bush.jpg")).Value).Value
            )
            .Value;
        var original = service.Prepare(analysis).Value;
        var scale = (PortraitOutput.Height - 1) / (analysis.Height - 1 + 2 * excess);
        var atEdge = original with
        {
            Transform = new PortraitTransform(scale, 0, 0, scale * excess),
        };
        var result = PortraitBackground.Apply(analysis, atEdge, PortraitBackgroundPolicy.Default);
        if (supported)
        {
            result.Should().Succeed();
            result.Value.Transform.Value.Should().Be(atEdge.Transform.Value);
            result.Value.Background.HasValue.Should().BeTrue();
        }
        else
            result.Should().Fail().Which.Code.Should().Be(FacialErrorCodes.MaskSampleOutsideSource);
    }
}
