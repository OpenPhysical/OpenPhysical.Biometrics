// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Packages;

internal sealed class ControlledExportStorage : IEnrollmentPackageStorage
{
    internal TaskCompletionSource<bool> Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource<Result<string, DesktopError>> Completed { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal CancellationToken Token { get; private set; }
    internal int Saves { get; private set; }
    internal Maybe<BiometricPackage> Package { get; private set; }
    internal Maybe<FingerprintRecord> Record =>
        Package.Map(value =>
            Incits378Codec
                .Parse(
                    value
                        .Artifacts.Single(artifact => artifact.Name == "fingerprints.fmr")
                        .Bytes.AsSpan()
                )
                .Value.Record
        );
    internal Maybe<CapturePerson> Person =>
        Package.Map(value => new CapturePerson(
            value.Label,
            value.ExternalId.GetValueOrDefault(string.Empty)
        ));

    public Task<Result<string, DesktopError>> SaveAsync(
        BiometricPackage package,
        PackageSecret secret,
        string folder,
        CancellationToken token
    )
    {
        Token = token;
        Package = package;
        Saves++;
        Started.TrySetResult(true);
        return Completed.Task;
    }

    public Task<Result<BiometricPackage, DesktopError>> OpenAsync(
        string path,
        PackageSecret secret,
        CancellationToken token
    ) =>
        Task.FromResult(
            Result.Failure<BiometricPackage, DesktopError>(
                new DesktopError("Test.Unused", "This fixture tests saving only.")
            )
        );
}
