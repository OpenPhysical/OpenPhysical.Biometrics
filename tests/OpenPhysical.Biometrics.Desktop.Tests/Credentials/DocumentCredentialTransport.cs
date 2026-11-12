// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class DocumentCredentialTransport(TemplateDocument document) : ICredentialTransport
{
    public Result<ImmutableArray<string>, DesktopError> Readers() =>
        ImmutableArray.Create("Sample reader", "Other sample reader");

    public Result<TemplateDocument, DesktopError> UseReader(
        string reader,
        Func<ICredentialCard, Result<TemplateDocument, DesktopError>> operation
    )
    {
        var bytes = ScriptedCredentialCard.Fingerprints(document.Record);
        return operation(new ScriptedCredentialCard([0x90, 0], [0x90, 0], [.. bytes, 0x90, 0]));
    }

    public Result<IDisposable, DesktopError> WatchRemoval(
        string reader,
        Func<UnitResult<DesktopError>> removed
    ) => Result.Success<IDisposable, DesktopError>(new SampleMonitor());

    private sealed class SampleMonitor : IDisposable
    {
        public void Dispose() { }
    }
}
