// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;

internal sealed class Fs50TemporaryPlane : IDisposable
{
    private const int Pixels = 800 * 750;
    internal byte[] Buffer { get; } = ArrayPool<byte>.Shared.Rent(Pixels);
    internal Span<byte> PixelsSpan => Buffer.AsSpan(0, Pixels);

    internal Fs50TemporaryPlane() => PixelsSpan.Clear();

    public void Dispose()
    {
        PixelsSpan.Clear();
        ArrayPool<byte>.Shared.Return(Buffer);
    }
}
