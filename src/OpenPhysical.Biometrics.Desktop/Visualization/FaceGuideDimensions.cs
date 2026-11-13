// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FaceGuideDimensions
{
    internal static ImmutableArray<FaceGuideSegment> Create(double width, double height) =>
        [
            new(new(0, 0), new(0, -12), FaceGuideTone.Framing),
            new(new(width, 0), new(width, -12), FaceGuideTone.Framing),
            new(new(0, -8), new(width, -8), FaceGuideTone.Framing),
            new(new(0, -8), new(8, -12), FaceGuideTone.Framing),
            new(new(0, -8), new(8, -4), FaceGuideTone.Framing),
            new(new(width, -8), new(width - 8, -12), FaceGuideTone.Framing),
            new(new(width, -8), new(width - 8, -4), FaceGuideTone.Framing),
            new(new(width, 0), new(width + 12, 0), FaceGuideTone.Framing),
            new(new(width, height), new(width + 12, height), FaceGuideTone.Framing),
            new(new(width + 8, 0), new(width + 8, height), FaceGuideTone.Framing),
            new(new(width + 8, 0), new(width + 4, 8), FaceGuideTone.Framing),
            new(new(width + 8, 0), new(width + 12, 8), FaceGuideTone.Framing),
            new(new(width + 8, height), new(width + 4, height - 8), FaceGuideTone.Framing),
            new(new(width + 8, height), new(width + 12, height - 8), FaceGuideTone.Framing),
        ];
}
