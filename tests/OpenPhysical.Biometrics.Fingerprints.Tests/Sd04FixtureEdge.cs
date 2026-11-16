// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using CSharpFunctionalExtensions;

internal static class Sd04FixtureEdge
{
    internal static Result<T> Run<T>(Func<Result<T>> read) =>
        Result.Try(read, _ => "FixtureInputInvalid").Bind(result => result);
}
