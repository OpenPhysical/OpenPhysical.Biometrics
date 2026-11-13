// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

internal static class PortraitBackgroundMatte
{
    internal static Result<byte[], FacialError> Create(
        PortraitPreparationResult portrait,
        PortraitPersonMask mask,
        PortraitBackgroundPolicy policy
    ) =>
        PortraitFaceOutline
            .Create(portrait.Faces[0], portrait.Transform.Value)
            .Bind(outline => Build(portrait.Transform.Value, mask, policy, outline));

    private static Result<byte[], FacialError> Build(
        PortraitTransform transform,
        PortraitPersonMask mask,
        PortraitBackgroundPolicy policy,
        System.Collections.Immutable.ImmutableArray<PortraitPoint> outline
    )
    {
        var confidence = new float[PortraitOutput.Width * PortraitOutput.Height];
        var face = new byte[confidence.Length];
        try
        {
            for (var y = 0; y < PortraitOutput.Height; y++)
            for (var x = 0; x < PortraitOutput.Width; x++)
            {
                var sample = Sample(mask, transform.InverseUnchecked(new(x, y)));
                if (sample.IsFailure)
                    return sample.Error;
                confidence[y * PortraitOutput.Width + x] = sample.Value;
                face[y * PortraitOutput.Width + x] = PortraitFaceOutline.Contains(outline, x, y)
                    ? (byte)1
                    : (byte)0;
            }
            return PortraitMatteBoundary.Refine(
                confidence,
                face,
                PortraitOutput.Width,
                PortraitOutput.Height,
                policy
            );
        }
        finally
        {
            Array.Clear(confidence);
            Array.Clear(face);
        }
    }

    private static Result<float, FacialError> Sample(PortraitPersonMask mask, PortraitPoint point)
    {
        const double edgeRoundoff = 1e-7;
        if (
            point.X < -edgeRoundoff
            || point.Y < -edgeRoundoff
            || point.X > mask.SourceWidth - 1 + edgeRoundoff
            || point.Y > mask.SourceHeight - 1 + edgeRoundoff
        )
            return mask.Sample(point);
        return mask.Sample(
            new PortraitPoint(
                Math.Clamp(point.X, 0, mask.SourceWidth - 1),
                Math.Clamp(point.Y, 0, mask.SourceHeight - 1)
            )
        );
    }
}
