// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class Fs50WholeContactWindowEvidence : IDisposable
{
    private readonly Fs50TemporaryPlane changed = new();
    private readonly Fs50TemporaryPlane unchanged = new();
    internal byte[] Changed => changed.Buffer;
    internal ReadOnlySpan<byte> Static => unchanged.PixelsSpan;
    internal Span<byte> StaticOutput => unchanged.PixelsSpan;

    public void Dispose()
    {
        changed.Dispose();
        unchanged.Dispose();
    }
}
