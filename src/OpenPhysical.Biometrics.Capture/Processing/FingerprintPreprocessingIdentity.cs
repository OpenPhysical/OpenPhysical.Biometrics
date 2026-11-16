// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Globalization;
using CSharpFunctionalExtensions;

/// <summary>Verifies version-bound saved artifact associations without running image algorithms.</summary>
public static class FingerprintPreprocessingIdentity
{
    /// <summary>Hashes validated source fields and independently verified companion digests.</summary>
    /// <param name="source">Validated acquisition identity.</param>
    /// <param name="originalDigest">Original Gray8 SHA-256.</param>
    /// <param name="processedDigest">Processing Gray8 SHA-256.</param>
    /// <param name="maskDigest">Geometry and mask SHA-256.</param>
    /// <param name="policyDigest">Version-specific policy SHA-256.</param>
    /// <param name="revision">Nonnegative processing revision.</param>
    /// <param name="version">Saved implementation identifier.</param>
    /// <returns>The canonical artifact identity or invalid-argument failure.</returns>
    public static Result<string, CaptureError> Create(
        FingerprintProcessingSource source,
        string originalDigest,
        string processedDigest,
        string maskDigest,
        string policyDigest,
        long revision,
        string version
    )
    {
        if (
            source is null
            || revision < 0
            || !CaptureDeviceDescriptor.ValidText(version)
            || !ValidDigest(originalDigest)
            || !ValidDigest(processedDigest)
            || !ValidDigest(maskDigest)
            || !ValidDigest(policyDigest)
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Processing companion identity fields are invalid."
            );
        return CaptureProcessingDigest.Fields(
            version,
            originalDigest.ToLowerInvariant(),
            processedDigest.ToLowerInvariant(),
            maskDigest.ToLowerInvariant(),
            policyDigest.ToLowerInvariant(),
            revision.ToString(CultureInfo.InvariantCulture),
            source.Sequence.ToString(CultureInfo.InvariantCulture),
            source.CapturedAt.UtcTicks.ToString(CultureInfo.InvariantCulture),
            source.DeviceId,
            source.Exposure.ToString(CultureInfo.InvariantCulture),
            source.CorrectionVersion,
            source.CalibrationDigest
        );
    }

    internal static bool ValidDigest(string value) =>
        value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);
}
