// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class PackageSessionSecret : IDisposable
{
    private readonly object gate = new();
    private Maybe<PackageSecret> secret;
    private bool disposed;
    private long generation;
    internal Maybe<PackageSecret> Current
    {
        get
        {
            lock (gate)
                return disposed ? Maybe<PackageSecret>.None : secret;
        }
    }
    internal long Generation
    {
        get
        {
            lock (gate)
                return generation;
        }
    }

    internal Result<long, DesktopError> Begin()
    {
        lock (gate)
            return disposed
                ? new DesktopError("Package.SessionDisposed", "Package protection has stopped.")
                : generation;
    }

    internal UnitResult<DesktopError> Replace(byte[] utf8, long expected)
    {
        var created = PackageSecret.Create(utf8);
        if (created.IsFailure)
            return new DesktopError(created.Error.Code, created.Error.Message);
        lock (gate)
        {
            if (disposed || generation != expected)
            {
                created.Value.Dispose();
                return new DesktopError(
                    "Package.Locked",
                    "Package protection was locked. Try again."
                );
            }
            secret.Tap(value => value.Dispose());
            secret = created.Value;
            generation++;
            return UnitResult.Success<DesktopError>();
        }
    }

    internal long Lock()
    {
        lock (gate)
        {
            secret.Tap(value => value.Dispose());
            secret = Maybe<PackageSecret>.None;
            return ++generation;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            Lock();
        }
    }
}
