// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

// Small constructors for the failures this library reports.
internal static class FingerprintChecks
{
    internal static Result<T, FingerprintError> Fail<T>(string code, string message) =>
        Result.Failure<T, FingerprintError>(new FingerprintError(code, message));

    internal static UnitResult<FingerprintError> Require(
        bool condition,
        string code,
        string message
    ) =>
        condition
            ? UnitResult.Success<FingerprintError>()
            : UnitResult.Failure(new FingerprintError(code, message));
}
