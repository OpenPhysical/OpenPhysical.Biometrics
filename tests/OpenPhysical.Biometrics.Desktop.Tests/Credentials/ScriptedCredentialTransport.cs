// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class ScriptedCredentialTransport(ICredentialCard card) : ICredentialTransport
{
    public Result<ImmutableArray<string>, DesktopError> Readers() =>
        ImmutableArray.Create("scripted reader");

    public Result<TemplateDocument, DesktopError> UseReader(
        string reader,
        Func<ICredentialCard, Result<TemplateDocument, DesktopError>> operation
    ) => operation(card);

    public Result<IDisposable, DesktopError> WatchRemoval(
        string reader,
        Func<UnitResult<DesktopError>> removed
    ) => new ScriptedMonitor();

    private sealed class ScriptedMonitor : IDisposable
    {
        public void Dispose() { }
    }
}
