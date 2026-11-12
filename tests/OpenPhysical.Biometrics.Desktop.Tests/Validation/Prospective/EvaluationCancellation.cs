// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;

internal static class EvaluationCancellation
{
    internal static UnitResult<DesktopError> Check(CancellationToken token) =>
        token.IsCancellationRequested
            ? UnitResult.Failure(new DesktopError("Cancelled", "The evaluation was canceled."))
            : UnitResult.Success<DesktopError>();
}
