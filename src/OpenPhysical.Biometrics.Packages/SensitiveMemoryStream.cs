// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

internal sealed class SensitiveMemoryStream : MemoryStream
{
    protected override void Dispose(bool disposing)
    {
        if (disposing && TryGetBuffer(out var segment) && segment.Array is byte[] bytes)
            Array.Clear(bytes);
        base.Dispose(disposing);
    }
}
