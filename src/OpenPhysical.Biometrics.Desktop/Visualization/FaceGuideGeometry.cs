// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Biometrics.Facial;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FaceGuideGeometry
{
    internal static ImmutableArray<FaceGuideSegment> Rectangle(
        double left,
        double top,
        double right,
        double bottom,
        FaceGuideTone tone
    ) =>
        [
            new(new(left, top), new(right, top), tone),
            new(new(right, top), new(right, bottom), tone),
            new(new(right, bottom), new(left, bottom), tone),
            new(new(left, bottom), new(left, top), tone),
        ];

    internal static FaceImageGuides Map(FaceImageGuides guides, PortraitTransform transform) =>
        guides with
        {
            SourceCoordinates = true,
            Segments =
            [
                .. guides.Segments.Select(line =>
                    line with
                    {
                        Start = transform.Inverse(line.Start).Value,
                        End = transform.Inverse(line.End).Value,
                    }
                ),
            ],
        };
}
