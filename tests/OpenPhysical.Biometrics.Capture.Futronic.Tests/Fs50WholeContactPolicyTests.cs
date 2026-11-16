// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactPolicyTests
{
    [TestCase(0, 16, 4000)]
    [TestCase(129, 16, 4000)]
    [TestCase(32, 0, 4000)]
    [TestCase(32, 33, 4000)]
    [TestCase(32, 16, 0)]
    [TestCase(32, 16, 600001)]
    public void InvalidSpatialAndAreaChoicesFailBeforeObservation(int window, int step, int area) =>
        Fs50WholeContactPolicy
            .Create(
                new Fs50WholeContactOptions
                {
                    WindowSize = window,
                    GridStep = step,
                    MinimumUncorroboratedArea = area,
                },
                Fs50EvidencePolicy.Default,
                Fs50EmptySensorPolicy.Default,
                FingerCapturePolicy.Default
            )
            .Error.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);

    [Test]
    public void StaticAndChangedThresholdsCannotOverlap()
    {
        var overlapping = Fs50EmptySensorPolicy
            .Create(
                new()
                {
                    MaximumStableMeanAbsoluteDifference = 3,
                    MinimumPresentationMeanAbsoluteDifference = 2,
                }
            )
            .Value;
        Fs50WholeContactPolicy
            .Create(new(), Fs50EvidencePolicy.Default, overlapping, FingerCapturePolicy.Default)
            .Error.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
    }
}
