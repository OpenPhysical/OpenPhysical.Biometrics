// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;

internal static class RenderedMinutiaComponents
{
    internal static List<List<Point>> Create(IEnumerable<Point> points)
    {
        var remaining = points.Select(p => ((int)p.X, (int)p.Y)).ToHashSet();
        var groups = new List<List<Point>>();
        while (remaining.Count > 0)
        {
            var pending = new Queue<(int X, int Y)>();
            pending.Enqueue(remaining.First());
            remaining.Remove(pending.Peek());
            var group = new List<Point>();
            while (pending.TryDequeue(out var p))
            {
                group.Add(new Point(p.X + .5, p.Y + .5));
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    if (remaining.Remove((p.X + dx, p.Y + dy)))
                        pending.Enqueue((p.X + dx, p.Y + dy));
            }
            groups.Add(group);
        }
        return groups;
    }
}
