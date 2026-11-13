// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class AcquisitionPolicyPackageTests
{
    [Test]
    public async Task SavedOriginalAcquisitionPolicyRetainsTheExactDelayAndCaptureChoices()
    {
        var policy = new CaptureAcquisitionPolicy(CapturePolicySettings.Default, 1.3);
        var capture = policy.ApplyTo(ProcessingStorageFixtures.Capture());
        var folder = ProcessingStorageFixtures.Folder();
        var saved = await new TemplateStorage().SaveAsync(
            UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value,
            [capture],
            folder,
            CancellationToken.None
        );
        saved.Should().Succeed();
        var loaded = await new TemplateStorage().LoadAsync(saved.Value, CancellationToken.None);
        loaded.Should().Succeed();
        var actual = loaded.Value.Images.Single().Processing.Value.OriginalAcquisitionPolicy.Value;
        actual.ToJson().GetRawText().Should().Be(policy.ToJson().GetRawText());
        System.IO.Directory.Delete(folder, true);
    }
}
