// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ContactContourPolicyTests
{
    [TestCase(-1, 8)]
    [TestCase(129, 8)]
    [TestCase(32, -1)]
    [TestCase(32, 33)]
    public void InvalidContextAndCutGuardChoicesAreRejected(int margin, int guard) =>
        Fs50ContactContourPolicy
            .Create(new() { ContextMargin = margin, ModifiedBoundaryGuard = guard })
            .Error.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);

    [Test]
    public void CancellationDoesNotPublishPartialGeometry()
    {
        var pixels = new byte[800 * 750];
        pixels[100] = 1;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Fs50ContactContourGeometry
            .Create(pixels, pixels, Fs50ContactContourPolicy.Default, cancelled.Token)
            .Error.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
    }
}
