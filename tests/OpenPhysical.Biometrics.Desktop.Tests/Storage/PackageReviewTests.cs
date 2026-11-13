// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Packages;

[TestFixture]
internal sealed class PackageReviewTests
{
    [AvaloniaTest]
    public async Task EncryptedReopenRestoresVerifiedViewsWithoutAssessingOrExtractingAgain()
    {
        var prepared = await PackageReviewFixtures.Fingerprints();
        await using var context = prepared.Context;
        var package = prepared.Package;
        var counts = context.Counts;
        using var secret = EnrollmentExportFixtures.Secret();
        var encrypted = BiometricPackageCodec
            .Seal(package, secret, PackageProtectionPolicy.Default)
            .Value;
        var opened = BiometricPackageCodec
            .Open(encrypted, secret, PackageProtectionPolicy.Default)
            .Value;
        var result = PackageReviewDocument.FromPackage(opened);
        result.Should().Succeed();
        result.Value.Label.Should().Be("Example person");
        var fingerprint = result.Value.Fingerprints.Value;
        fingerprint.Images.Should().HaveCount(1);
        fingerprint
            .Images[0]
            .GrayPixels.Should()
            .Equal(context.Capture.Captured.Value.Processing.Value.Artifact.Pixels);
        using var model = PackageReviewViewModel.Create(result.Value).Value;
        model.FingerprintScene.HasValue.Should().BeTrue();
        model.FingerprintScene.Value.Minutiae.Should().Equal(fingerprint.Record.Views[0].Minutiae);
        context.Counts.Should().Be(counts);
        model.Dispose();
        model.FingerprintScene.HasNoValue.Should().BeTrue();
    }

    [TestCase("missing")]
    [TestCase("hash")]
    [TestCase("pixels")]
    [TestCase("geometry")]
    public async Task AdvertisedFingerprintCompanionsMustPassTheExistingAssociations(string defect)
    {
        var prepared = await PackageReviewFixtures.Fingerprints();
        await using var context = prepared.Context;
        var changed = PackageReviewFixtures.WithCompanionDefect(prepared.Package, defect);
        PackageReviewDocument
            .FromPackage(changed)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
    }

    [AvaloniaTest]
    public void FaceRecordDecodesWithoutModelInferenceAndProfileFindingsStaySeparate()
    {
        var package = PackageReviewFixtures.Face(expression: 2);
        var reviewed = PackageReviewDocument.FromPackage(package);
        reviewed.Should().Succeed();
        reviewed.Value.Face.HasValue.Should().BeTrue();
        reviewed
            .Value.FaceProfileFindings.Should()
            .Contain(value => value.Code == "NeutralExpression");
        using var model = PackageReviewViewModel.Create(reviewed.Value).Value;
        model.FaceScene.HasValue.Should().BeTrue();
        model.FaceScene.Value.Width.Should().Be(808);
        model.FaceScene.Value.Height.Should().Be(1024);
        model.Dispose();
        model.FaceScene.HasNoValue.Should().BeTrue();
    }

    [TestCase("{\"Sections\":[\"face\",\"fingerprints\"]}")]
    [TestCase("{}")]
    public void InspectionRequiresThePersonPlanAndItsAdvertisedObjects(string evidence)
    {
        var source = PackageReviewFixtures.Face();
        var package = BiometricPackage
            .Create(
                source.PersonId,
                source.Label,
                Maybe<string>.None,
                System.Text.Encoding.UTF8.GetBytes(evidence),
                source.Artifacts
            )
            .Value;
        PackageReviewDocument
            .FromPackage(package)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Package.Objects");
    }
}
