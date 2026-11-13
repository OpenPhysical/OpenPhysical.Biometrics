// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class StudioVerificationFactoryFixture(
    params StudioVerificationBackendFixture[] backends
) : IStudioVerificationBackendFactory
{
    internal int Opens { get; private set; }

    public Task<Result<IStudioVerificationBackend, DesktopError>> OpenAsync(CancellationToken token)
    {
        if (Opens >= backends.Length)
            return Task.FromResult(
                Result.Failure<IStudioVerificationBackend, DesktopError>(
                    new("Verification.Setup", "No verification runtime is available.")
                )
            );
        return Task.FromResult(
            Result.Success<IStudioVerificationBackend, DesktopError>(backends[Opens++])
        );
    }
}
