// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

[TestFixture]
internal sealed class QualificationPolicyRoleTests
{
    [Test]
    public async Task WiderEditedContextPreservesOriginalFeaturesAndRecordedQualification()
    {
        var capture = SyntheticRecordedQualificationFixtures.Capture();
        var evidence = capture.Processing.Value;
        var (folder, path, _) = await PackageTestFiles.SaveAsync(WidenContext(capture));
        try
        {
            var loaded = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
            loaded.Should().Succeed();
            var actual = loaded.Value.Images.Single().Processing.Value;
            actual.Contour.Value.Policy.Options.ContextMargin.Should().Be(48);
            actual.Contour.Value.FeatureMask.Should().Equal(evidence.Contour.FeatureMask);
            actual.OriginalAcquisitionPolicy.Value.Settings.ContextMargin.Should().Be(32);
            actual
                .OriginalQualification.Value.PolicyDigest.Should()
                .Be(evidence.OriginalQualification.Value.PolicyDigest);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task OriginalQualificationPolicyCannotDisagreeWithTheOriginalDecisionSnapshot()
    {
        var capture = SyntheticRecordedQualificationFixtures.Capture();
        var original = capture.Processing.Value.OriginalAcquisitionPolicy.Value;
        var settings = original
            .Settings.WithWholeContact(
                original.Settings.WholeContactOptions with
                {
                    MinimumUncorroboratedArea =
                        original.Settings.WholeContactOptions.MinimumUncorroboratedArea + 1,
                }
            )
            .Value;
        capture = capture with
        {
            Processing = capture.Processing.Value with
            {
                OriginalAcquisitionPolicy = new CaptureAcquisitionPolicy(
                    settings,
                    original.DelaySeconds
                ),
            },
        };
        var folder = ProcessingStorageFixtures.Folder();
        var saved = await new TemplateStorage().SaveAsync(
            UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value,
            [capture],
            folder,
            CancellationToken.None
        );
        saved.Should().Fail().Which.Code.Should().Be("Storage.Manifest");
        Directory.Exists(folder).Should().BeFalse();
    }

    private static CapturedFingerprint WidenContext(CapturedFingerprint capture)
    {
        var evidence = capture.Processing.Value;
        var contour = evidence
            .Contour.WithContext(
                Fs50ContactContourPolicy.Create(new() { ContextMargin = 48 }).Value,
                CancellationToken.None
            )
            .Value;
        var region = contour.ToProcessingRegion(false).Value;
        var artifact = FingerprintPreprocessing
            .Process(
                capture.Frame,
                region.Mask,
                evidence.Artifact.Policy,
                evidence.ProcessingRevision + 1,
                CancellationToken.None
            )
            .Value;
        return capture with
        {
            Processing = evidence with
            {
                Contour = contour,
                ProcessingRegion = region,
                Artifact = artifact,
            },
        };
    }
}
