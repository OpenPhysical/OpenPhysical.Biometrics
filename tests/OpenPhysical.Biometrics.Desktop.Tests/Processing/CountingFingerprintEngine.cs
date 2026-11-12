// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.NBIS;

internal sealed class CountingFingerprintEngine(IFingerprintEngine inner) : IFingerprintEngine
{
    internal int Extractions { get; private set; }
    internal ImmutableArray<byte> LastPixels { get; private set; } = [];

    public FingerprintTemplate Extract(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int dotsPerInch
    )
    {
        Extractions++;
        LastPixels = ImmutableArray.CreateRange(pixels.ToArray());
        return inner.Extract(pixels, width, height, dotsPerInch);
    }

    public int Match(FingerprintTemplate probe, FingerprintTemplate gallery) =>
        inner.Match(probe, gallery);
}
