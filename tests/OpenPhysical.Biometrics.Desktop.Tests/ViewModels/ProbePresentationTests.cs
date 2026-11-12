// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class ProbePresentationTests
{
    [AvaloniaTest]
    public async Task SuccessfulRegistrationShowsOnlyReferencePointsAndReassignmentClearsThem()
    {
        var dependencies = new ControlledInspectionDependencies();
        dependencies.Release();
        var capture = dependencies.Captured.Value;
        var points = Constellation();
        capture = capture with { View = capture.View with { Minutiae = points } };
        dependencies.SetCapture(capture);
        dependencies.Document = dependencies.Document with
        {
            Record = dependencies.Document.Record with { Views = [capture.View] },
        };
        var view = NewView(dependencies);
        await view.LoadCommand.ExecuteAsync(default);
        await view.CompareCommand.ExecuteAsync(default);
        view.ProbeScene.Value.Minutiae.Should().BeEmpty();
        view.ProbeScene.Value.ReferenceOverlay.Should().HaveCount(points.Length);
        view.AlignmentStatus.Should().Contain("aligned");
        view.Report.Should().Contain("correspondences");
        view.AssignedProbeFinger = FingerChoice.For(FingerPosition.LeftIndex);
        view.ProbeScene.Value.ReferenceOverlay.Should().BeEmpty();
        dependencies.Captured.Value.View.Should().BeSameAs(capture.View);
        await view.StopAsync();
    }

    [AvaloniaTest]
    public async Task VerificationShowsTheCaptureImageWithoutItsExtractedMinutiae()
    {
        var dependencies = new ControlledInspectionDependencies();
        var view = NewView(dependencies);
        view.ProbeScene.HasValue.Should().BeTrue();
        view.ProbeScene.Value.Image.HasValue.Should().BeTrue();
        view.ProbeScene.Value.Minutiae.Should().BeEmpty();
        dependencies.Captured.Value.View.Minutiae.Should().NotBeEmpty();
        dependencies.SetCapture(Maybe<CapturedFingerprint>.None);
        view.ProbeScene.HasNoValue.Should().BeTrue();
        dependencies.SetLiveFrame(
            new DisplayedFingerprintFrame(StorageFixtures.Capture().Frame, default)
        );
        view.ProbeScene.Value.Image.HasValue.Should().BeTrue();
        view.ProbeScene.Value.Minutiae.Should().BeEmpty();
        view.ProbeScene.Value.ReferenceOverlay.Should().BeEmpty();
        dependencies.SetLiveFrame(Maybe<DisplayedFingerprintFrame>.None);
        view.ProbeScene.HasNoValue.Should().BeTrue();
        dependencies.SetCapture(StorageFixtures.Capture(FingerPosition.LeftIndex));
        view.ProbeScene.Value.Minutiae.Should().BeEmpty();
        view.ShowReferenceOverlay.Should().BeTrue();
        await view.StopAsync();
        view.ProbeScene.HasNoValue.Should().BeTrue();
    }

    private static InspectionViewModel NewView(ControlledInspectionDependencies dependencies) =>
        new(
            dependencies,
            new DesktopFilePicker(),
            dependencies,
            new TemplateComparison(new FingerprintComparisonService(dependencies)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );

    private static ImmutableArray<FingerprintMinutia> Constellation()
    {
        var locations = new (ushort X, ushort Y)[]
        {
            (10, 10),
            (100, 15),
            (20, 115),
            (110, 90),
            (60, 60),
            (30, 40),
            (80, 105),
            (105, 45),
            (45, 95),
            (75, 20),
            (15, 75),
            (55, 10),
        };
        return locations
            .Select(
                (point, index) =>
                    new FingerprintMinutia(1, point.X, point.Y, (byte)(index * 13), 90)
            )
            .ToImmutableArray();
    }
}
