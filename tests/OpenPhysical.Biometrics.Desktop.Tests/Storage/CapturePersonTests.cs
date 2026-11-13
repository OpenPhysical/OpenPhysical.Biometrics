// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class CapturePersonTests
{
    [Test]
    public void NameOrLabelIsRequiredAndIdentifiersStayOptional()
    {
        CapturePerson.Create("", "").Should().Fail().Which.Code.Should().Be("Session.Person");
        CapturePerson.Create("alias", "").Should().Succeed();
        CapturePerson
            .Create("  Lab alias  ", "  tester-17  ")
            .Should()
            .Succeed()
            .Which.Should()
            .Be(new CapturePerson("Lab alias", "tester-17"));
        CapturePerson.Create("line\nbreak", "").Should().Fail();
        CapturePerson.Create(new string('a', 129), "").Should().Fail();
    }
}
