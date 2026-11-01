// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

internal static class SensitiveBytes
{
    // Clears the buffer once it has been used, even when the use faults.
    internal static T With<T>(byte[] bytes, Func<byte[], T> use)
    {
        try
        {
            return use(bytes);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }
}
