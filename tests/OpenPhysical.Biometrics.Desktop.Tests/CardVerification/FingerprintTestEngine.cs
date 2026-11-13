// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.NBIS;

namespace OpenPhysical.Middleware.Tests;

// The native matcher is the controlled boundary; signed records and policy execution remain real.
internal sealed class FingerprintTestEngine(int score) : IFingerprintEngine
{
    internal int Matches { get; private set; }

    public FingerprintTemplate Extract(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int dotsPerInch
    ) => new(width, height, [], "synthetic-engine-boundary");

    public int Match(FingerprintTemplate probe, FingerprintTemplate gallery)
    {
        Matches++;
        return score;
    }
}
