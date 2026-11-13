// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Packages;

[TestFixture]
internal sealed class PersonWorkspaceTests
{
    [AvaloniaTest]
    public async Task ANewPersonHasFaceAndFingerprintSectionsWithOneSharedIdentity()
    {
        using var host = DesktopServices.CreateHost();
        var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
        var enrollment = workspace.Export.Enrollment;
        enrollment.Sections.Select(section => section.Id).Should().Equal("face", "fingerprints");
        enrollment.Fingerprints.TwoFingers.Should().BeTrue();
        enrollment.Person.Name = "Sample person";
        enrollment.Ready.Should().BeFalse();
        await workspace.Export.StopAsync();
    }

    [AvaloniaTest]
    public async Task OnePersonCanReviewRetakeAndReopenFaceAndFingerprintsTogether()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        context.Person.Identifier = "example-17";
        await context.PrepareFaceAsync();
        context.Face.Complete.Should().BeFalse();
        context.Enrollment.FaceActive.Should().BeTrue();
        context.Enrollment.AcceptCommand.CanExecute(default).Should().BeFalse();
        await context.AcceptFaceAsync();
        context.Enrollment.FingerprintsActive.Should().BeTrue();
        await CaptureFingerprintsAsync(context);
        await RetakeFaceAsync(context);
        await RetakeOneFingerAsync(context);
        context.Export.Ready.Should().BeTrue();
        var counts = context.Fingerprints.Counts;
        var face = context.Face.Accepted.Value;
        var raw = context.Camera.LastFrame.Value;
        var path = await context.SaveAsync();
        await context.Export.ClearPersonCommand.ExecuteAsync(default);
        context.Person.Name.Should().BeEmpty();
        context.Person.Identifier.Should().BeEmpty();
        context.Face.Complete.Should().BeFalse();
        context.Enrollment.Fingerprints.AcceptedCaptures.Should().BeEmpty();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        raw.Image.CopyRgbBytes().Should().Fail();
        context.Protection.IsUnlocked.Should().BeTrue();
        using var secret = EnrollmentExportFixtures.Secret();
        var opened = await context.Storage.OpenAsync(path, secret, CancellationToken.None);
        opened.Should().Succeed();
        VerifyReopened(opened.Value, face.ObjectBytes);
        context.Fingerprints.Counts.Should().Be(counts);
    }

    private static async Task CaptureFingerprintsAsync(PersonWorkspaceContext context)
    {
        await context.Fingerprints.AcceptAsync(
            context.Fingerprints.NativeCapture(FingerPosition.RightIndex)
        );
        context.Enrollment.FingerprintsActive.Should().BeTrue();
        await context.Fingerprints.AcceptAsync(
            context.Fingerprints.NativeCapture(FingerPosition.LeftIndex)
        );
        context.Enrollment.Ready.Should().BeTrue();
    }

    private static async Task RetakeFaceAsync(PersonWorkspaceContext context)
    {
        var fingers = context.Enrollment.Fingerprints.AcceptedCaptures;
        await context.SelectAsync("face");
        await context.Face.Actions.Retake.ExecuteAsync(default);
        context.Face.Complete.Should().BeFalse();
        context.Enrollment.Fingerprints.AcceptedCaptures.Should().Equal(fingers);
        await context.PrepareFaceAsync();
        await context.AcceptFaceAsync();
        context.Enrollment.Fingerprints.AcceptedCaptures.Should().Equal(fingers);
    }

    private static async Task RetakeOneFingerAsync(PersonWorkspaceContext context)
    {
        var face = context.Face.Accepted.Value;
        var left = context.Enrollment.Fingerprints.AcceptedCaptures[1];
        await context.SelectAsync("fingerprints");
        await context.Enrollment.Fingerprints.PrimarySlotCommand.ExecuteAsync(default);
        await context.Enrollment.Fingerprints.ResetCaptureCommand.ExecuteAsync(default);
        context.Face.Accepted.Value.Should().BeSameAs(face);
        context.Enrollment.Fingerprints.AcceptedCaptures.Should().Equal(left);
        await context.Fingerprints.AcceptAsync(
            context.Fingerprints.NativeCapture(FingerPosition.RightIndex)
        );
        context.Face.Accepted.Value.Should().BeSameAs(face);
        context.Enrollment.Fingerprints.AcceptedCaptures[1].Should().BeSameAs(left);
    }

    private static void VerifyReopened(
        BiometricPackage package,
        System.Collections.Immutable.ImmutableArray<byte> face
    )
    {
        package.Label.Should().Be("Example person");
        package.ExternalId.Value.Should().Be("example-17");
        package.Artifacts.Single(value => value.Name == "face.bin").Bytes.Should().Equal(face);
        package
            .Artifacts.Where(value => value.Name.StartsWith("face.", StringComparison.Ordinal))
            .Select(value => value.Name)
            .Should()
            .BeEquivalentTo("face.bin", "face.json");
        var review = PackageReviewDocument.FromPackage(package);
        review.Should().Succeed();
        review.Value.Face.HasValue.Should().BeTrue();
        review
            .Value.Fingerprints.Value.Record.Views.Select(view => view.FingerPosition)
            .Should()
            .Equal((byte)FingerPosition.RightIndex, (byte)FingerPosition.LeftIndex);
        using var model = PackageReviewViewModel.Create(review.Value).Value;
        model.FaceScene.HasValue.Should().BeTrue();
        model.FingerprintScene.HasValue.Should().BeTrue();
    }
}
