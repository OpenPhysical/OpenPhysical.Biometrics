// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class DesktopEdge
{
    internal static async Task<Result<Maybe<T>, DesktopError>> TryOptionalFileAsync<T>(
        Func<Task<T>> operation,
        string code,
        string message
    )
    {
        try
        {
            return Maybe.From(await operation().ConfigureAwait(false));
        }
        catch (FileNotFoundException)
        {
            return Maybe<T>.None;
        }
        catch (DirectoryNotFoundException)
        {
            return Maybe<T>.None;
        }
        catch (OperationCanceledException)
        {
            return new DesktopError("Cancelled", "The operation was cancelled.");
        }
        catch (Exception error)
        {
            return new DesktopError(code, $"{message} {error.Message}");
        }
    }

    internal static Result<T, DesktopError> Try<T>(Func<T> operation, string code, string message)
    {
        try
        {
            return operation();
        }
        catch (Exception error)
        {
            return new DesktopError(code, $"{message} {error.Message}");
        }
    }

    internal static async Task<Result<T, DesktopError>> TryAsync<T>(
        Func<Task<T>> operation,
        string code,
        string message
    )
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new DesktopError("Cancelled", "The operation was cancelled.");
        }
        catch (Exception error)
        {
            return new DesktopError(code, $"{message} {error.Message}");
        }
    }
}
