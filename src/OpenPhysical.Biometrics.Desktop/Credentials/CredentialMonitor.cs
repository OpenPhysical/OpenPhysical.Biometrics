// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class CredentialMonitor
{
    private Maybe<IDisposable> current;

    internal UnitResult<DesktopError> Start(
        CredentialReaderService reader,
        string name,
        Func<UnitResult<DesktopError>> removed
    )
    {
        var stopped = Stop();
        if (stopped.IsFailure)
            return stopped;
        var watching = reader.WatchRemoval(name, removed);
        if (watching.IsFailure)
            return UnitResult.Failure(watching.Error);
        current = Maybe.From<IDisposable>(watching.Value);
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Stop()
    {
        if (current.HasNoValue)
            return UnitResult.Success<DesktopError>();
        var value = current.Value;
        current = Maybe<IDisposable>.None;
        var result = DesktopEdge.Try(
            () =>
            {
                value.Dispose();
                return true;
            },
            "Credential.Monitor",
            "The credential monitor could not stop."
        );
        return result.IsSuccess
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(result.Error);
    }
}
