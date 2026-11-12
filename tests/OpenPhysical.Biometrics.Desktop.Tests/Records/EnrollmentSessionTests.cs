// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class EnrollmentSessionTests
{
    [Test]
    public void AcceptingEachPlannedFingerPreservesCapturesAndRecordOrder()
    {
        var right = StorageFixtures.Capture(FingerPosition.RightIndex);
        var left = StorageFixtures.Capture(FingerPosition.LeftIndex);
        var session = EnrollmentSession.Create([right.Finger, left.Finger]).Value;
        session.NextFinger.Value.Should().Be(right.Finger);
        session = session.Accept(right).Value;
        session.Complete.Should().BeFalse();
        session.NextFinger.Value.Should().Be(left.Finger);
        session = session.Accept(left).Value;
        session.Complete.Should().BeTrue();
        session.Selection().Value.Should().Equal(right, left);
        session.Accept(right).Should().Fail().Which.Code.Should().Be("Session.Complete");
    }

    [Test]
    public void WrongFingerCannotReplaceAnAcceptedCapture()
    {
        var right = StorageFixtures.Capture(FingerPosition.RightIndex);
        var session = EnrollmentSession
            .Create([right.Finger, FingerPosition.LeftIndex])
            .Value.Accept(right)
            .Value;
        session
            .Accept(StorageFixtures.Capture(right.Finger))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Session.Finger");
        session.Accepted.Single().Should().BeSameAs(right);
    }

    [Test]
    public void ChangingThePlanRetainsOnlyApplicableAcceptedFingers()
    {
        var right = StorageFixtures.Capture(FingerPosition.RightIndex);
        var session = EnrollmentSession
            .Create([right.Finger, FingerPosition.LeftIndex])
            .Value.Accept(right)
            .Value;
        var single = session.Replan([right.Finger]).Value;
        single.Complete.Should().BeTrue();
        single.Selection().Value.Single().Should().BeSameAs(right);
        session.Replan([FingerPosition.LeftMiddle]).Value.Accepted.Should().BeEmpty();
        EnrollmentSession
            .Create([right.Finger, right.Finger])
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Session.Plan");
    }
}
