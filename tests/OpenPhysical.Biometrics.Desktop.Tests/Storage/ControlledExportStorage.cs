// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal sealed class ControlledExportStorage : ITemplateSessionStorage
{
    internal TaskCompletionSource<bool> Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource<Result<string, DesktopError>> Completed { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal CancellationToken Token { get; private set; }

    internal int Saves { get; private set; }
    internal Maybe<FingerprintRecord> Record { get; private set; }

    internal Maybe<CapturePerson> Person { get; private set; }
    internal ImmutableArray<CapturedFingerprint> Accepted { get; private set; } = [];

    public Task<Result<string, DesktopError>> SaveSessionAsync(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        CapturePerson person,
        string folder,
        CancellationToken cancellationToken
    )
    {
        Token = cancellationToken;
        Record = record;
        Person = person;
        Accepted = captures;
        Saves++;
        Started.TrySetResult(true);
        return Completed.Task;
    }

    public Task<Result<TemplateDocument, DesktopError>> LoadAsync(
        string path,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            Result.Failure<TemplateDocument, DesktopError>(
                new DesktopError("Test.Unused", "This fixture tests saving only.")
            )
        );
}
