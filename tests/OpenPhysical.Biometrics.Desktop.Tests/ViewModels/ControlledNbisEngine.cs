// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.NBIS;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ControlledNbisEngine(
    IFingerprintEngine inner,
    Func<FingerprintImage, bool> beforeExtraction
) : IFingerprintEngine
{
    public FingerprintTemplate Extract(ReadOnlySpan<byte> pixels, int width, int height, int dpi)
    {
        beforeExtraction(new FingerprintImage(pixels, width, height, dpi));
        return inner.Extract(pixels, width, height, dpi);
    }

    public int Match(FingerprintTemplate probe, FingerprintTemplate reference) =>
        inner.Match(probe, reference);
}
