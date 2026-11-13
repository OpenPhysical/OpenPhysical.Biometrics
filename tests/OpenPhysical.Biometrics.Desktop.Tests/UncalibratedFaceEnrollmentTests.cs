// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Packages;

[TestFixture]
internal sealed class UncalibratedFaceEnrollmentTests
{
    [AvaloniaTest]
    public async Task AReviewedFaceCanBeAcceptedSavedAndReopenedWithoutClaimingOpticalProof()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        context.Enrollment.Plan = CapturePlanChoice.Face;
        (await context.Enrollment.ActivateAsync(true)).Should().Succeed();
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        context
            .Face.OpticalQualificationApplied(
                context.Camera.Source.Id,
                context.Face.SelectedMode,
                true
            )
            .Should()
            .Succeed();
        await context.Face.Actions.Capture.ExecuteAsync(default);
        await FaceCaptureTestActions.OverrideRejectedAsync(context.Face);
        context.Face.ReviewConfirmed = true;
        context.Face.AcceptCommand.CanExecute(default).Should().BeTrue();
        await context.Enrollment.AcceptCommand.ExecuteAsync(default);
        context.Face.Complete.Should().BeTrue();
        context.Face.Accepted.Value.Acquisition.OpticalResolutionConfirmed.Should().BeFalse();
        var acknowledged = context.Face.Accepted.Value.CaptureOverride.Value;
        var saved = await context.SaveAsync();
        await context.Export.ClearPersonCommand.ExecuteAsync(default);
        using var secret = EnrollmentExportFixtures.Secret();
        var opened = await context.Storage.OpenAsync(saved, secret, CancellationToken.None);
        opened.Should().Succeed();
        AssertUnknownEvidence(opened.Value, acknowledged);
        PackageReviewDocument.FromPackage(opened.Value).Should().Succeed();
    }

    [AvaloniaTest]
    public async Task AcceptedOpticalReceiptSurvivesLaterSetupRevocation()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        context.Enrollment.Plan = CapturePlanChoice.Face;
        await context.PrepareFaceAsync();
        var receipt = context.CameraSetup.Current.Value;
        await context.AcceptFaceAsync();
        context.Face.Accepted.Value.OpticalQualification.Value.Should().BeSameAs(receipt);
        await context.CameraSetup.ForgetCommand.ExecuteAsync(default);
        context.CameraSetup.Current.HasNoValue.Should().BeTrue();
        context.Face.Accepted.Value.Acquisition.OpticalResolutionConfirmed.Should().BeTrue();
        var path = await context.SaveAsync();
        using var secret = EnrollmentExportFixtures.Secret();
        var opened = await context.Storage.OpenAsync(path, secret, CancellationToken.None);
        opened.Should().Succeed();
        using var evidence = JsonDocument.Parse(opened.Value.EvidenceJson.AsMemory());
        var saved = evidence.RootElement.GetProperty("CameraQualification")[0];
        saved.GetProperty("SourceId").GetString().Should().Be(receipt.SourceId);
        saved.GetProperty("TargetPixels").GetDouble().Should().Be(receipt.TargetPixels);
        using var face = JsonDocument.Parse(
            opened.Value.Artifacts.Single(value => value.Name == "face.json").Bytes.AsMemory()
        );
        face.RootElement.GetProperty("opticalResolutionConfirmed").GetBoolean().Should().BeTrue();
        face.RootElement.GetProperty("opticalQualification").GetArrayLength().Should().Be(1);
    }

    private static bool AssertUnknownEvidence(
        BiometricPackage package,
        FacialCaptureOverride acknowledged
    )
    {
        using var json = JsonDocument.Parse(
            package.Artifacts.Single(value => value.Name == "face.json").Bytes.AsMemory()
        );
        json.RootElement.GetProperty("opticalResolutionConfirmed").GetBoolean().Should().BeFalse();
        AssertOverrideEvidence(json.RootElement, acknowledged);
        json.RootElement.GetProperty("acquisitionFindings")
            .EnumerateArray()
            .Should()
            .Contain(value => value.GetProperty("Code").GetString() == "OpticalResolutionUnknown");
        using var person = JsonDocument.Parse(package.EvidenceJson.AsMemory());
        person.RootElement.GetProperty("CameraQualification").GetArrayLength().Should().Be(0);
        var record = Incits385Codec.Parse(
            package.Artifacts.Single(value => value.Name == "face.bin").Bytes.AsSpan()
        );
        record.Should().Succeed();
        record.Value.Record.Images[0].ImageInformation.Quality.Should().Be(0);
        return true;
    }

    private static bool AssertOverrideEvidence(JsonElement face, FacialCaptureOverride acknowledged)
    {
        face.GetProperty("portraitReviewed").GetBoolean().Should().BeTrue();
        face.GetProperty("fullFrontalReviewed").GetBoolean().Should().BeFalse();
        var saved = face.GetProperty("captureOverride")[0];
        saved.GetProperty("SourceSha256").GetString().Should().Be(acknowledged.SourceSha256);
        saved
            .GetProperty("AcknowledgedAt")
            .GetDateTimeOffset()
            .Should()
            .Be(acknowledged.AcknowledgedAt);
        saved
            .GetProperty("FindingCodes")
            .EnumerateArray()
            .Select(v => v.GetString())
            .Should()
            .BeEquivalentTo(acknowledged.FindingCodes);
        face.GetProperty("failedCaptureChecks")
            .EnumerateArray()
            .Select(v => v.GetProperty("Code").GetString())
            .Should()
            .BeEquivalentTo(acknowledged.FindingCodes);
        var mouth = face.GetProperty("mouth");
        var tolerance = mouth.GetProperty("Policy").GetProperty("MaximumOpeningRatio").GetDouble();
        tolerance.Should().Be(acknowledged.MouthPolicy.MaximumOpeningRatio);
        mouth
            .GetProperty("Measurement")[0]
            .GetProperty("MaximumOpeningRatio")
            .GetDouble()
            .Should()
            .BeGreaterThan(tolerance);
        return true;
    }
}
