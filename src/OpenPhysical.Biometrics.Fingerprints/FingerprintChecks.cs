// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

// Shared value checks and constructors for the failures this library reports.
internal static class FingerprintChecks
{
    internal static UnitResult<FingerprintError> Finger(FingerPosition finger) =>
        Require(
            Enum.IsDefined(finger),
            FingerprintErrorCodes.InvalidFinger,
            "The finger position is not an ANSI finger code."
        );

    internal static UnitResult<FingerprintError> ViewNumber(byte viewNumber) =>
        Require(
            viewNumber <= 15,
            FingerprintErrorCodes.InvalidViewNumber,
            "The view number must be from zero through fifteen."
        );

    internal static UnitResult<FingerprintError> Impression(FingerprintImpression impression) =>
        Require(
            Enum.IsDefined(impression),
            FingerprintErrorCodes.InvalidImpression,
            "The impression type is not supported."
        );

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
