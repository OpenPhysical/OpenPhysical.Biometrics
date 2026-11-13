// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture.Futronic;

[TestFixture]
internal sealed class SpatialAdmissionPackageTests
{
    [Test]
    public async Task SyntheticSpatialRecordRoundTripsWithoutInventingBackgroundEvidence()
    {
        var capture = SyntheticRecordedQualificationFixtures.SpatialCapture();
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
            actual.PolicyDigest.Should().Be(expected.PolicyDigest);
            actual.RidgeEvidence.Should().Be(expected.RidgeEvidence);
            actual.Decision.Should().Be(Fs50WholeContactDecision.SpatialCapture);
            actual.ReferenceDigest.HasNoValue.Should().BeTrue();
            actual.ReferenceKeyDigest.HasNoValue.Should().BeTrue();
            actual.QualifiedAnchorDigest.HasNoValue.Should().BeTrue();
            actual
                .Metrics.Should()
                .Be(Unknown(details.Contour.Value.Statistics.SelectedContactArea));
            actual
                .ObservedMetrics.Should()
                .Be(Unknown(details.ObservedContactMask.Count(v => v != 0)));
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [TestCase("Reference")]
    [TestCase("Anchor")]
    [TestCase("ContactCorroboration")]
    [TestCase("ObservedCorroboration")]
    [TestCase("ExposureOnly")]
    public async Task RehashedSpatialRecordCannotClaimBackgroundEvidenceOrCapturedExposureOnly(
        string contradiction
    )
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            SyntheticRecordedQualificationFixtures.SpatialCapture()
        );
        try
        {
            var processing = manifest["Images"]![0]!["Processing"]!;
            var record = processing["OriginalQualification"]!;
            _ = Contradict(record, contradiction);
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
            System.IO.Directory.Delete(folder, true);
        }
    }

    private static bool Contradict(JsonNode record, string contradiction)
    {
        if (contradiction == "Reference")
        {
            record["ReferenceDigest"] = new string('a', 64);
            record["ReferenceKeyDigest"] = record["Identity"]!["SessionKeyDigest"]!.DeepClone();
        }
        if (contradiction == "Anchor")
            record["QualifiedAnchorDigest"] = new string('a', 64);
        if (contradiction is "ContactCorroboration" or "ObservedCorroboration")
        {
            var metrics = record[
                contradiction == "ContactCorroboration" ? "Metrics" : "ObservedMetrics"
            ]!;
            var area = metrics["ContactArea"]!.GetValue<int>();
            metrics["CorroboratedArea"] = area;
            metrics["UncorroboratedExteriorArea"] = 0;
            metrics["UnobservedExteriorArea"] = 0;
            metrics["LargestUncorroboratedExteriorArea"] = 0;
        }
        if (contradiction == "ExposureOnly")
            record["Decision"] = "SpatialExposure";
        return true;
    }

    private static Fs50WholeContactMetrics Unknown(int area) => new(area, 0, area, 0, area, area);
}
