// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Facial;

internal sealed class PersonWorkspaceContext : IAsyncDisposable
{
    private readonly string root = EnrollmentExportFixtures.Folder();
    private readonly PortraitPreparer portraits = new(NullLogger<PortraitPreparer>.Instance);
    private readonly EnrollmentOperatorContext fingerprints;

    internal PersonWorkspaceContext(bool deferOpen = false)
        : this(new FaceTestCameraProvider(deferOpen)) { }

    internal PersonWorkspaceContext(FaceTestCameraProvider camera)
    {
        Camera = camera;
        Face = new(
            Camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            Person,
            NullLogger<FaceCaptureViewModel>.Instance
        );
        CameraSetup = new(Face, new CameraQualificationStorage(Path.Combine(root, "camera.json")));
        fingerprints = new(CreateExport);
        Export.Folder = Path.Combine(root, "packages");
    }

    internal PersonEnrollmentViewModel Person { get; } = new();
    internal FaceTestCameraProvider Camera { get; }
    internal FaceCaptureViewModel Face { get; }
    internal CameraSetupViewModel CameraSetup { get; }
    internal EnrollmentPackageStorage Storage { get; } = new();
    internal PackageProtectionViewModel Protection { get; } =
        EnrollmentExportFixtures.UnlockedProtection();
    internal ExportViewModel Export => fingerprints.Export;
    internal EnrollmentViewModel Enrollment => Export.Enrollment;
    internal EnrollmentOperatorContext Fingerprints => fingerprints;

    internal async Task PrepareFaceAsync()
    {
        (await Enrollment.ActivateAsync(true)).Should().Succeed();
        await FaceCaptureTestActions.ManualPreviewAsync(Face);
        CameraSetup.OperatorId = "fixture-operator";
        CameraSetup.TargetConfirmed = true;
        CameraSetup.TargetPixels = 240;
        await CameraSetup.SaveCommand.ExecuteAsync(default);
        CameraSetup.Current.HasValue.Should().BeTrue();
        await Face.Actions.Capture.ExecuteAsync(default);
        await FaceCaptureTestActions.OverrideRejectedAsync(Face);
    }

    internal async Task AcceptFaceAsync()
    {
        Face.ReviewConfirmed = true;
        Enrollment.AcceptCommand.CanExecute(default).Should().BeTrue();
        await Enrollment.AcceptCommand.ExecuteAsync(default);
        Face.Complete.Should().BeTrue();
    }

    internal Task SelectAsync(string id) =>
        Enrollment.SelectSectionCommand.ExecuteAsync(
            Enrollment.Sections.Single(section => section.Id == id)
        );

    internal async Task<string> SaveAsync()
    {
        await Export.SaveCommand.ExecuteAsync(default);
        Export.Status.Should().StartWith("Saved:");
        var path = System.IO.Directory.EnumerateFiles(Export.Folder, "*.opbio").Single();
        System
            .IO.Directory.EnumerateFiles(Export.Folder, "*", SearchOption.AllDirectories)
            .Should()
            .Equal(path);
        return path;
    }

    private ExportViewModel CreateExport(
        CaptureViewModel capture,
        InspectionViewModel inspection
    ) =>
        new(
            capture,
            Storage,
            EnrollmentTestSuggestions.IndexPair(),
            inspection,
            Person,
            [EnrollmentSectionRegistration.Face(Face)],
            Protection,
            new DesktopFilePicker(),
            CameraSetup
        );

    public async ValueTask DisposeAsync()
    {
        await fingerprints.DisposeAsync();
        portraits.Dispose();
        if (System.IO.Directory.Exists(root))
            System.IO.Directory.Delete(root, true);
    }
}
