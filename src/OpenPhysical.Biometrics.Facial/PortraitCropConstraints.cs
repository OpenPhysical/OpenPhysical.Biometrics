// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

// Applies the Full Frontal placement limits and source-coverage limits to a region.
internal static class PortraitCropConstraints
{
    private const int Center = PortraitOutput.Width / 2;

    internal static FeasibleRegion Apply(
        FeasibleRegion initial,
        PortraitHeadFrame frame,
        int width,
        int height
    ) => Coverage(Placement(initial, frame), frame, width, height);

    // Approximate horizontal centering uses the documented two-percent tolerance.
    private static FeasibleRegion Placement(FeasibleRegion region, PortraitHeadFrame frame) =>
        region
            .Clip(Math.Abs(frame.Nose.X - frame.Mouth.X), 0, 19.2)
            .Clip(frame.Eye.Y - frame.Crown.Y, -1, 0)
            .Clip(frame.Chin.Y - frame.Eye.Y, 1, PortraitOutput.Height - 1)
            .Clip(frame.Center - frame.Left.X, 0, Center)
            .Clip(frame.Right.X - frame.Center, 0, PortraitOutput.Width - 1 - Center);

    private static FeasibleRegion Coverage(
        FeasibleRegion region,
        PortraitHeadFrame frame,
        int width,
        int height
    )
    {
        var rotation = frame.Rotation;
        var sourceX = rotation.A * frame.Center - rotation.B * frame.Eye.Y;
        var sourceY = rotation.B * frame.Center + rotation.A * frame.Eye.Y;
        foreach (
            var corner in new[]
            {
                new PortraitPoint(0, 0),
                new(PortraitOutput.Width - 1, 0),
                new(0, PortraitOutput.Height - 1),
                new(PortraitOutput.Width - 1, PortraitOutput.Height - 1),
            }
        )
        {
            var x = rotation.A * (corner.X - Center) - rotation.B * corner.Y;
            var y = rotation.B * (corner.X - Center) + rotation.A * corner.Y;
            region = region
                .Clip(-sourceX, -rotation.B, x)
                .Clip(sourceX - width + 1, rotation.B, -x)
                .Clip(-sourceY, rotation.A, y)
                .Clip(sourceY - height + 1, -rotation.A, -y);
        }
        return region;
    }
}
