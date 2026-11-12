// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class ReopenedProcessingSceneTests
{
    [AvaloniaTest]
    public async Task ReopenedReferenceShowsVerifiedPixelsAndSourceCoordinateOutline()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var (folder, path, _) = await PackageTestFiles.SaveAsync(capture);
        using var services = new ScoringTestServices();
        var inspection = NewInspection(services, path);
        try
        {
            await inspection.LoadCommand.ExecuteAsync(default);
            inspection.Scene.HasValue.Should().BeTrue();
            var scene = inspection.Scene.Value;
            scene.Width.Should().Be(capture.Frame.Width);
            scene.Height.Should().Be(capture.Frame.Height);
            scene.GrayPixels.Should().Equal(capture.Processing.Value.Artifact.Pixels);
            scene.Minutiae.Should().Equal(capture.View.Minutiae);
            SceneGeometryExpectations
                .OutlineBounds(scene)
                .Should()
                .Be(
                    SceneGeometryExpectations.MaskBounds(
                        capture.Processing.Value.Contour.FeatureMask,
                        capture.Frame.Width
                    )
                );
            services.Counts.Should().Be((0, 0, 0));
        }
        finally
        {
            (await inspection.StopAsync()).Should().Succeed();
            Directory.Delete(folder, true);
        }
    }

    private static InspectionViewModel NewInspection(ScoringTestServices services, string path) =>
        new(
            new TemplateStorage(),
            new DesktopFilePicker(),
            new ControlledInspectionDependencies(),
            new TemplateComparison(new FingerprintComparisonService(services.Engine)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        )
        {
            FilePath = path,
        };
}
