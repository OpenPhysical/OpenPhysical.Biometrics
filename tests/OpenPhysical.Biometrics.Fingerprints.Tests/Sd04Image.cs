// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;
using Org.BouncyCastle.Security;

internal sealed record Sd04Image(
    string Id,
    string RawPath,
    string Sha256,
    int Width,
    int Height,
    int DotsPerInch
)
{
    internal Result<FingerprintImage> Load() => Sd04FixtureEdge.Run(Read);

    private Result<FingerprintImage> Read()
    {
        var pixels = File.ReadAllBytes(RawPath);
        try
        {
            var actual = Convert.ToHexString(DigestUtilities.CalculateDigest("SHA-256", pixels));
            return actual.Equals(Sha256, StringComparison.OrdinalIgnoreCase)
                ? Result.Success(new FingerprintImage(pixels, Width, Height, DotsPerInch))
                : Result.Failure<FingerprintImage>("FixtureChecksumInvalid");
        }
        finally
        {
            Array.Clear(pixels);
        }
    }
}
