// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto;

[SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "IO, ZIP, JSON and cryptographic exceptions terminate at this edge."
)]
internal static class PackageEdge
{
    internal static Result<T, PackageError> Try<T>(Func<T> call, string code, string message)
    {
        try
        {
            return call();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Describe(error, code, message);
        }
    }

    internal static Result<T, PackageError> Bind<T>(
        Func<Result<T, PackageError>> call,
        string code,
        string message
    ) => Try(call, code, message).Bind(value => value);

    internal static async Task<Result<T, PackageError>> Async<T>(
        Func<Task<Result<T, PackageError>>> call,
        string code,
        string message
    )
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Describe(error, code, message);
        }
    }

    private static PackageError Describe(Exception error, string code, string message) =>
        error switch
        {
            OperationCanceledException => new(
                PackageErrorCodes.Cancelled,
                "The operation was cancelled."
            ),
            InvalidCipherTextException => new(
                PackageErrorCodes.AuthenticationFailed,
                "The passphrase is incorrect or the package was changed."
            ),
            _ => new(code, message),
        };
}
