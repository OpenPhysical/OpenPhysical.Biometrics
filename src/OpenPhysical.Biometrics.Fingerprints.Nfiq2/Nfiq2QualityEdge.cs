// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NFIQ2;

internal static class Nfiq2QualityEdge
{
    internal static Result<Nfiq2Assessment, FingerprintError> Assess(
        Func<Nfiq2Assessment> operation
    )
    {
        try
        {
            return operation();
        }
        catch (Nfiq2Exception error)
            when (error.NativeStatus == Nfiq2NativeStatus.AssessmentFailure)
        {
            return Result.Failure<Nfiq2Assessment, FingerprintError>(
                new FingerprintError(
                    FingerprintErrorCodes.QualityImageNotAssessable,
                    $"NFIQ 2 could not assess the usable fingerprint area (upstream code {error.UpstreamErrorCode})."
                )
            );
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Result.Failure<Nfiq2Assessment, FingerprintError>(
                new FingerprintError(
                    FingerprintErrorCodes.QualityEngineFailed,
                    "The NFIQ 2 quality engine failed."
                )
            );
        }
    }
}
