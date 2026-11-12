// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal static class Fs50RidgeProfile
{
    private readonly record struct Direction(int X, int Y, double Cosine, double Sine);

    internal static int SamplingMargin(int maximumShift)
    {
        var normal = 15.5 + maximumShift;
        return (int)Math.Floor(Math.Sqrt(normal * normal + 100) + 0.5);
    }

    internal static bool Create(
        ReadOnlySpan<byte> pixels,
        int x,
        int y,
        double angle,
        Span<double> profile,
        Fs50EvidenceOptions options
    )
    {
        var direction = new Direction(x, y, Math.Cos(angle), Math.Sin(angle));
        Span<double> first = stackalloc double[32];
        Span<double> second = stackalloc double[32];
        Span<double> firstSums = stackalloc double[32];
        Fill(pixels, direction, profile, first, second, firstSums);
        var shift = BestShift(first, second, options);
        return shift.HasValue
            && (
                shift.Value == 0
                || Align(pixels, direction, shift.Value, profile, first, firstSums, options)
            );
    }

    private static bool Fill(
        ReadOnlySpan<byte> pixels,
        Direction direction,
        Span<double> profile,
        Span<double> first,
        Span<double> second,
        Span<double> firstSums
    )
    {
        for (var i = 0; i < profile.Length; i++)
        {
            double left = 0,
                right = 0;
            for (var tangent = -2; tangent <= 2; tangent++)
            {
                left += Sample(pixels, direction, i - 15.5, tangent - 8);
                right += Sample(pixels, direction, i - 15.5, tangent + 8);
            }
            firstSums[i] = left;
            first[i] = left / 5;
            second[i] = right / 5;
            profile[i] = (left + right) / 10;
        }
        return true;
    }

    private static Maybe<int> BestShift(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        Fs50EvidenceOptions options
    )
    {
        var best = double.NegativeInfinity;
        var selected = 0;
        for (
            var shift = -options.MaximumParallelShift;
            shift <= options.MaximumParallelShift;
            shift++
        )
        {
            var length = first.Length - Math.Abs(shift);
            var correlation = Fs50RidgeCorrelation.Coefficient(
                first.Slice(Math.Max(0, shift), length),
                second.Slice(Math.Max(0, -shift), length)
            );
            if (correlation > best || correlation == best && Preferred(shift, selected))
            {
                best = correlation;
                selected = shift;
            }
        }
        return best >= options.MinimumParallelCorrelation ? Maybe.From(selected) : Maybe<int>.None;
    }

    private static bool Preferred(int candidate, int selected) =>
        Math.Abs(candidate) < Math.Abs(selected)
        || Math.Abs(candidate) == Math.Abs(selected) && candidate < selected;

    private static bool Align(
        ReadOnlySpan<byte> pixels,
        Direction direction,
        int shift,
        Span<double> profile,
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> firstSums,
        Fs50EvidenceOptions options
    )
    {
        if (!CanAlign(direction, shift))
            return false;
        // Read actual end samples instead of padding a shortened agreement window.
        Span<double> second = stackalloc double[32];
        for (var i = 0; i < profile.Length; i++)
        {
            double right = 0;
            for (var tangent = -2; tangent <= 2; tangent++)
                right += Sample(pixels, direction, i - 15.5 - shift, tangent + 8);
            second[i] = right / 5;
            profile[i] = (firstSums[i] + right) / 10;
        }
        return Fs50RidgeCorrelation.Coefficient(first, second)
            >= options.MinimumParallelCorrelation;
    }

    private static bool CanAlign(Direction direction, int shift) =>
        Inside(direction, -15.5 - shift, 6)
        && Inside(direction, -15.5 - shift, 10)
        && Inside(direction, 15.5 - shift, 6)
        && Inside(direction, 15.5 - shift, 10);

    private static bool Inside(Direction direction, double normal, int tangent)
    {
        var (x, y) = Position(direction, normal, tangent);
        return x is >= 0 and < 800 && y is >= 0 and < 750;
    }

    private static byte Sample(
        ReadOnlySpan<byte> pixels,
        Direction direction,
        double normal,
        int tangent
    )
    {
        var (x, y) = Position(direction, normal, tangent);
        return pixels[(y * 800) + x];
    }

    private static (int X, int Y) Position(Direction direction, double normal, int tangent)
    {
        // One tie rule prevents half-pixel steps from repeating alternate source pixels.
        var x = (int)
            Math.Floor(
                direction.X + (normal * direction.Cosine) - (tangent * direction.Sine) + 0.5
            );
        var y = (int)
            Math.Floor(
                direction.Y + (normal * direction.Sine) + (tangent * direction.Cosine) + 0.5
            );
        return (x, y);
    }
}
