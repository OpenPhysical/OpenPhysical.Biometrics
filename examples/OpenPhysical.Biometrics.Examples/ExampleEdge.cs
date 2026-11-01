// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;

// The single edge where the throwing NBIS and NFIQ engines meet the example workflows.
[SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Third-party native engines throw; this edge converts every failure to a result."
)]
internal static class ExampleEdge
{
    private const string Failure =
        "The workflow failed. Check input geometry and native runtime installation.";

    internal static Result<T, string> TryBind<T>(Func<Result<T, string>> call)
    {
        try
        {
            return call();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Result.Failure<T, string>(Failure);
        }
    }
}
