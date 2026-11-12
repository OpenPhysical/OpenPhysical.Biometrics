// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

internal sealed class ControlledPresenceClock : TimeProvider
{
    private long timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => timestamp;

    internal long Advance(int milliseconds) =>
        timestamp += TimeSpan.FromMilliseconds(milliseconds).Ticks;
}
