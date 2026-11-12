// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ProspectiveHistoricalContactTests
{
    [TestCase(false, 0, "TooLight", "Capture.ContactTooLight")]
    [TestCase(false, 0, "InsufficientRidgeEvidence", "Capture.UnclearContact")]
    [TestCase(false, 0, "None", "Capture.NoFinger")]
    [TestCase(true, 2, "None", "Capture.MultipleFingers")]
    public void HistoricalContactRejectionRetainsItsActualObservedCause(
        bool present,
        int count,
        string issue,
        string expected
    )
    {
        var descriptor = JsonSerializer.SerializeToElement(
            new
            {
                legacyPresent = present,
                legacyComponentCount = count,
                legacyIssue = issue,
            }
        );
        var result = ProspectiveHistoricalContact.Read(descriptor);
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Code.Should().Be(expected);
    }

    [Test]
    public void ValidHistoricalContactRemainsAvailable()
    {
        var result = ProspectiveHistoricalContact.Read(
            JsonSerializer.SerializeToElement(
                new
                {
                    legacyPresent = true,
                    legacyComponentCount = 1,
                    legacyIssue = "None",
                }
            )
        );
        result.IsSuccess.Should().BeTrue();
        result.Value.HasNoValue.Should().BeTrue();
    }

    [TestCase(true, 0, "None")]
    [TestCase(false, 1, "None")]
    [TestCase(true, 1, "Unrecognized")]
    public void InconsistentOrUnknownOracleEvidenceAborts(bool present, int count, string issue)
    {
        var result = ProspectiveHistoricalContact.Read(
            JsonSerializer.SerializeToElement(
                new
                {
                    legacyPresent = present,
                    legacyComponentCount = count,
                    legacyIssue = issue,
                }
            )
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Evaluation.LegacyEvidence");
    }
}
