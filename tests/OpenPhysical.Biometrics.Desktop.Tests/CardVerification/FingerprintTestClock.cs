// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Tests;

internal sealed class FingerprintTestClock : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = VerificationBundleFixture.Now;

    public override DateTimeOffset GetUtcNow() => Now;
}
