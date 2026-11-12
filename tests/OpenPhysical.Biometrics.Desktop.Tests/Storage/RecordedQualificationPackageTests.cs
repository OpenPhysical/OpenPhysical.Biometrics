// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class RecordedQualificationPackageTests
{
    [Test]
    public async Task SyntheticRecordedQualificationPreservesOriginalPolicyAndReferenceIdentity()
    {
        var capture = SyntheticRecordedQualificationFixtures.Capture();
        var expected = capture.Processing.Value.OriginalQualification.Value;
        var (folder, path, _) = await PackageTestFiles.SaveAsync(capture);
        try
        {
            var loaded = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
            loaded.Should().Succeed();
            var details = loaded.Value.Images.Single().Processing.Value;
            var actual = details.OriginalQualification.Value;
            details.InputRole.Should().Be(CaptureProcessingInputRole.LiveSensor);
            actual.Identity.Should().Be(expected.Identity);
            actual.Decision.Should().Be(expected.Decision);
            actual.PolicyDigest.Should().Be(expected.PolicyDigest);
            actual.Policy.Options.Should().Be(expected.Policy.Options);
            actual.Policy.Evidence.Digest.Should().Be(expected.Policy.Evidence.Digest);
            actual.Policy.EmptySensor.Digest.Should().Be(expected.Policy.EmptySensor.Digest);
            actual.Policy.Presentation.Should().Be(expected.Policy.Presentation);
            actual.ReferenceDigest.HasValue.Should().BeTrue();
            actual.ReferenceDigest.Value.Should().Be(expected.ReferenceDigest.Value);
            actual.ReferenceKeyDigest.HasValue.Should().BeTrue();
            actual.ReferenceKeyDigest.Value.Should().Be(expected.ReferenceKeyDigest.Value);
            actual.QualifiedAnchorDigest.HasNoValue.Should().BeTrue();
            actual.Metrics.Should().Be(expected.Metrics);
            actual.RidgeEvidence.Should().Be(expected.RidgeEvidence);
            actual.ObservedMetrics.Should().Be(expected.ObservedMetrics);
            actual
                .ObservedMetrics.ContactArea.Should()
                .Be(details.ObservedContactMask.Count(value => value != 0));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestCase("Sequence")]
    [TestCase("ContactDigest")]
    [TestCase("Policy")]
    [TestCase("DriverVersion")]
    [TestCase("RidgeEvidence")]
    [TestCase("ObservedMetrics")]
    public async Task RehashedRecordedQualificationCannotContradictItsOriginalAcquisition(
        string field
    )
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            SyntheticRecordedQualificationFixtures.Capture()
        );
        try
        {
            var processing = manifest["Images"]![0]!["Processing"]!;
            _ = Tamper(manifest, processing, field);
            processing["SettingsSha256"] = PackageTestFiles.SettingsDigest(processing);
            await PackageTestFiles.WriteAsync(path, manifest);
            (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("Storage.Manifest");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static bool Tamper(JsonNode manifest, JsonNode processing, string field)
    {
        var qualification = processing["OriginalQualification"]!;
        if (field == "Sequence")
            qualification["Identity"]!["Sequence"] = 123456;
        if (field == "ContactDigest")
            qualification["Identity"]!["ContactDigest"] = new string('0', 64);
        if (field == "Policy")
        {
            var options = qualification["Policy"]!["Options"]!;
            options["MinimumUncorroboratedArea"] =
                options["MinimumUncorroboratedArea"]!.GetValue<int>() + 1;
        }
        if (field == "DriverVersion")
            manifest["Images"]![0]!["Provenance"]!["DriverVersion"] = "different-driver";
        if (field == "ObservedMetrics")
            qualification["ObservedMetrics"]!["ContactArea"] = 1;
        if (field == "RidgeEvidence")
            qualification["RidgeEvidence"]!["ObservedContactDigest"] = new string('0', 64);
        return true;
    }
}
