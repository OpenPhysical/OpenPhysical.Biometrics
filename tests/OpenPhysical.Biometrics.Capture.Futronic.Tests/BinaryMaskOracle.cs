// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Independent flood-fill oracle reads only public output masks.
internal static class BinaryMaskOracle
{
    internal static Result<ImmutableArray<ImmutableArray<int>>, CaptureError> Components(
        ReadOnlySpan<byte> mask,
        CancellationToken cancellationToken
    )
    {
        var remaining = mask.ToArray();
        var groups = ImmutableArray.CreateBuilder<ImmutableArray<int>>();
        for (var index = 0; index < remaining.Length; index++)
        {
            if (remaining[index] == 0)
                continue;
            var positions = new List<int> { index };
            remaining[index] = 0;
            for (var head = 0; head < positions.Count; head++)
            {
                var current = positions[head];
                int x = current % 800,
                    y = current / 800;
                foreach (var next in Neighbors(x, y))
                    if (remaining[next] != 0)
                    {
                        remaining[next] = 0;
                        positions.Add(next);
                    }
            }
            groups.Add([.. positions]);
        }
        return groups.ToImmutable();
    }

    internal static Result<ImmutableArray<ImmutableArray<int>>, CaptureError> Holes(
        ReadOnlySpan<byte> mask,
        CancellationToken cancellationToken
    )
    {
        var inverted = new byte[mask.Length];
        for (var i = 0; i < mask.Length; i++)
            inverted[i] = (byte)(1 - mask[i]);
        return Components(inverted, cancellationToken)
            .Map(groups =>
                groups
                    .Where(group => !group.Any(i => i % 800 is 0 or 799 || i / 800 is 0 or 749))
                    .ToImmutableArray()
            );
    }

    private static IEnumerable<int> Neighbors(int x, int y)
    {
        for (var yy = Math.Max(0, y - 1); yy <= Math.Min(749, y + 1); yy++)
        for (var xx = Math.Max(0, x - 1); xx <= Math.Min(799, x + 1); xx++)
            yield return yy * 800 + xx;
    }
}
