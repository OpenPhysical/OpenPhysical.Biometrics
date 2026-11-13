// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;

[TestFixture]
internal sealed class FaceGuideGeometryTests
{
    [AvaloniaTest]
    public async Task PortraitGuideLimitsUsePreparedCoordinatesAndActualDetectedEyes()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        context.Face.Image.ImageGuides.HasNoValue.Should().BeTrue();
        await context.PrepareFaceAsync();
        var output = context.Face.Image.ImageGuides.Value;
        output.SourceCoordinates.Should().BeFalse();
        output
            .Segments.Should()
            .Contain(line =>
                line.Start == new PortraitPoint(0, 192) && line.End == new PortraitPoint(480, 192)
            );
        output
            .Segments.Should()
            .Contain(line =>
                line.Start == new PortraitPoint(0, 320) && line.End == new PortraitPoint(480, 320)
            );
        context.Face.ReviewConfirmed = true;
        await context.Face.AcceptCommand.ExecuteAsync(default);
        var portrait = context.Face.Accepted.Value.Portrait;
        AssertActualEyeCrosshairs(output, portrait);
        AssertSuggestedRegion(output);
    }

    private static bool AssertActualEyeCrosshairs(
        FaceImageGuides guides,
        PortraitPreparationResult portrait
    )
    {
        var features = portrait.Faces.Single().Features.Value;
        foreach (var eye in new[] { features.ViewerLeftEye, features.ViewerRightEye })
        {
            var position = portrait.Transform.Value.Map(eye);
            guides
                .Segments.Should()
                .Contain(line =>
                    line.Start.X == position.X
                    && line.End.X == position.X
                    && line.Start.Y < position.Y
                    && line.End.Y > position.Y
                );
            guides
                .Segments.Should()
                .Contain(line =>
                    line.Start.Y == position.Y
                    && line.End.Y == position.Y
                    && line.Start.X < position.X
                    && line.End.X > position.X
                );
        }
        return true;
    }

    private static bool AssertSuggestedRegion(FaceImageGuides guides)
    {
        guides
            .Segments.Where(line => line.Tone == FaceGuideTone.Region)
            .Should()
            .Equal(
                new FaceGuideSegment(new(48, 48), new(432, 48), FaceGuideTone.Region),
                new FaceGuideSegment(new(432, 48), new(432, 528), FaceGuideTone.Region),
                new FaceGuideSegment(new(432, 528), new(48, 528), FaceGuideTone.Region),
                new FaceGuideSegment(new(48, 528), new(48, 48), FaceGuideTone.Region)
            );
        return true;
    }
}
