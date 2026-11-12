// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ProspectiveFailureTests
{
    [TestCase("QualityImageNotAssessable", "ImageNotAssessable")]
    [TestCase("Capture.ContactTooLight", "ContactRejected")]
    [TestCase("Capture.MultipleFingers", "ContactRejected")]
    [TestCase("Capture.NoReliableArea", "NoReliableArea")]
    [TestCase("capture.AmbiguousTissueRegion", "AmbiguousTissueRegion")]
    public void ExplicitCaptureInabilityHasAnAuditableReason(string code, string reason)
    {
        var result = ProspectiveFailure.Classify(new(code, "evidence"));
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(reason);
    }

    [TestCase("EngineFailed")]
    [TestCase("QualityEngineFailed")]
    [TestCase("Capture.Cancelled")]
    [TestCase("Evaluation.Input")]
    [TestCase("Capture.NoAssessableCandidates")]
    [TestCase("Capture.NoFeatures")]
    public void UnexpectedOperationalAndGenericErrorsAbort(string code)
    {
        var result = ProspectiveFailure.Classify(new(code, "failure"));
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(code);
    }

    [TestCase("final", false, false)]
    [TestCase("final", true, true)]
    [TestCase("development", false, true)]
    [TestCase("diagnostic", false, false)]
    public void ReservedFinalRequiresExplicitFrozenRelease(
        string role,
        bool authorized,
        bool allowed
    ) => ProspectiveFrozenProtocol.Access(role, authorized).IsSuccess.Should().Be(allowed);
}
