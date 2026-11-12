// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;

internal static class EvaluationEdge
{
    internal static Result<T, DesktopError> Try<T>(Func<T> work, string code, string message)
    {
        try
        {
            return work();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return new DesktopError(code, message);
        }
    }
}
