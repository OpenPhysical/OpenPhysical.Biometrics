// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal sealed class Fs50EmptySensorRun(Fs50EmptySensorOptions options, TimeProvider clock)
{
    private Maybe<Fs50EmptySensorKey> key;
    private byte[] anchor = [];
    private int[] sums = [];
    private long started,
        previous;
    private long sequence = -1;
    private int count;

    internal Maybe<Fs50EmptySensorReference> Observe(CaptureFrame frame, Fs50EmptySensorKey current)
    {
        var now = clock.GetTimestamp();
        if (key.HasValue && key.Value == current && frame.Sequence <= sequence)
            return Maybe<Fs50EmptySensorReference>.None;
        if (!Compatible(frame, current, now))
            _ = Begin(frame, current, now);
        else
            _ = Add(frame, now);
        var span = clock.GetElapsedTime(started, now);
        if (
            count < options.RequiredFrames
            || span.TotalMilliseconds < options.MinimumSpanMilliseconds
        )
            return Maybe<Fs50EmptySensorReference>.None;
        var pixels = new byte[sums.Length];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)Math.Round(sums[i] / (double)count);
        var reference = new Fs50EmptySensorReference(current, pixels, count, span);
        Array.Clear(pixels);
        _ = Clear();
        return reference;
    }

    internal bool Clear()
    {
        Array.Clear(anchor);
        Array.Clear(sums);
        anchor = [];
        sums = [];
        key = Maybe<Fs50EmptySensorKey>.None;
        count = 0;
        sequence = -1;
        return true;
    }

    private bool Compatible(CaptureFrame frame, Fs50EmptySensorKey current, long now) =>
        key.HasValue
        && key.Value == current
        && now >= previous
        && clock.GetElapsedTime(previous, now).TotalMilliseconds
            <= options.MaximumFrameGapMilliseconds
        && Fs50EmptySensorDifference.Mean(anchor, frame.Pixels.AsSpan(), [])
            <= options.MaximumStableMeanAbsoluteDifference;

    private bool Begin(CaptureFrame frame, Fs50EmptySensorKey current, long now)
    {
        _ = Clear();
        key = current;
        started = now;
        anchor = frame.Pixels.ToArray();
        sums = new int[anchor.Length];
        return Add(frame, now);
    }

    private bool Add(CaptureFrame frame, long now)
    {
        for (var i = 0; i < sums.Length; i++)
            sums[i] += frame.Pixels[i];
        count++;
        sequence = frame.Sequence;
        previous = now;
        return true;
    }
}
