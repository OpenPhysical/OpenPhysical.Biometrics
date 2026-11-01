// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

// The single boundary where third-party fingerprint engines, which report invalid input and
// native failures by throwing, meet the Result-based API.
internal static class FingerprintEdge
{
    internal static Result<T, FingerprintError> Try<T>(Func<T> call, string code, string message)
    {
        try
        {
            return call();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Result.Failure<T, FingerprintError>(new FingerprintError(code, message));
        }
    }
}
