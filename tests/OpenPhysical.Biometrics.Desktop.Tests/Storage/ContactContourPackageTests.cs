// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ContactContourPackageTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ReopenPreservesMeasuredInferredFeatureAndContextRoles(bool modified)
    {
        var capture = HullStorageFixtures.Capture(modified);
        var (folder, path, _) = await PackageTestFiles.SaveAsync(capture);
        try
        {
            var loaded = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
            loaded.Should().Succeed();
            var details = loaded.Value.Images.Single().Processing.Value;
            var contour = details.Contour.Value;
            var expected = capture.Processing.Value.Contour;
            contour.ContactMask.Should().Equal(expected.ContactMask);
            details
                .ObservedContactMask.Should()
                .Equal(capture.Processing.Value.ContactDiagnostics.ObservedContactMask);
            contour.FilledHoleMask.Should().Equal(expected.FilledHoleMask);
            contour.FeatureMask.Should().Equal(expected.FeatureMask);
            contour.SupportMask.Should().Equal(expected.SupportMask);
            contour.ContextMask.Should().Equal(expected.ContextMask);
            contour.UncertainMask.Should().Equal(expected.UncertainMask);
            details
                .ProcessingRegion.Value.GuardedContextMask.Should()
                .Equal(capture.Processing.Value.ProcessingRegion.GuardedContextMask);
            details.ProcessingRegion.Value.AppliedGuard.Should().Be(modified ? 8 : 0);
            details.Mask.Inner.Should().Equal(capture.Processing.Value.Artifact.Mask.Inner);
            details.Mask.Retained.Should().Equal(contour.ContextMask);
            var diagnostics = details.ContactDiagnostics.Value;
            var original = capture.Processing.Value.ContactDiagnostics;
            diagnostics.CandidateMask.HasNoValue.Should().BeTrue();
            diagnostics.PolicyDigest.Should().Be(original.PolicyDigest);
            diagnostics.Statistics.AcceptedArea.Should().Be(original.AcceptedArea);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
