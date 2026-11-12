// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;

internal static class EmptySensorTestFrames
{
    internal static async Task<ControlledCaptureRequest> PrimeAsync(
        ControlledCaptureDriver driver,
        ControlledCaptureClock clock
    )
    {
        var empty = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        var pending = await driver.NextAsync();
        for (var index = 0; index < 4; index++)
        {
            if (index > 0)
                clock.Advance(TimeSpan.FromMilliseconds(250));
            pending.Complete(empty).Should().BeTrue();
            pending = await driver.NextAsync();
        }
        return pending;
    }
}
