// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ControlledCaptureClock : TimeProvider
{
    private long timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref timestamp);

    internal long Advance(TimeSpan elapsed) => Interlocked.Add(ref timestamp, elapsed.Ticks);
}
