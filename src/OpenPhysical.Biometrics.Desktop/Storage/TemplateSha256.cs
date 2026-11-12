// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Org.BouncyCastle.Security;

internal static class TemplateSha256
{
    internal static string Of(byte[] bytes) =>
        Convert.ToHexString(DigestUtilities.CalculateDigest("SHA-256", bytes));
}
