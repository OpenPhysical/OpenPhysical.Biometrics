// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

internal static class PackageNames
{
    internal static bool Valid(string name)
    {
        if (
            string.IsNullOrEmpty(name)
            || name.Length > 240
            || name.StartsWith('/')
            || name.EndsWith('/')
        )
            return false;
        var parts = name.Split('/');
        return parts.Length <= 8
            && parts.All(ValidPart)
            && !string.Equals(name, "manifest.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidPart(string part)
    {
        if (
            part.Length == 0
            || part.Length > 128
            || part is "." or ".."
            || part.EndsWith('.')
            || part.Equals(".git", StringComparison.OrdinalIgnoreCase)
        )
            return false;
        if (!part.All(value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_' or '.'))
            return false;
        var stem = part.Split('.')[0].ToUpperInvariant();
        return stem is not ("CON" or "PRN" or "AUX" or "NUL")
            && !(
                stem.Length == 4
                && (
                    stem.StartsWith("COM", StringComparison.Ordinal)
                    || stem.StartsWith("LPT", StringComparison.Ordinal)
                )
                && stem[3] is >= '1' and <= '9'
            );
    }
}
