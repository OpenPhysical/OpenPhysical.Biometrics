// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using PCSC;
using PCSC.Monitoring;

internal sealed class PcscCredentialTransport : ICredentialTransport
{
    public Result<ImmutableArray<string>, DesktopError> Readers() =>
        DesktopEdge.Try(
            () =>
            {
                using var context = ContextFactory.Instance.Establish(SCardScope.System);
                return context.GetReaders().ToImmutableArray();
            },
            "Credential.Readers",
            "Card readers could not be enumerated."
        );

    public Result<TemplateDocument, DesktopError> UseReader(
        string reader,
        Func<ICredentialCard, Result<TemplateDocument, DesktopError>> operation
    ) =>
        DesktopEdge
            .Try(
                () =>
                {
                    using var context = ContextFactory.Instance.Establish(SCardScope.System);
                    using var card = context.ConnectReader(
                        reader,
                        SCardShareMode.Shared,
                        SCardProtocol.Any
                    );
                    using var transaction = card.Transaction(SCardReaderDisposition.Leave);
                    return operation(new PcscCredentialCard(card));
                },
                "Credential.Connection",
                "The inserted credential could not be read."
            )
            .Bind(result => result);

    public Result<IDisposable, DesktopError> WatchRemoval(
        string reader,
        Func<UnitResult<DesktopError>> removed
    ) =>
        DesktopEdge.Try<IDisposable>(
            () =>
            {
                var monitor = MonitorFactory.Instance.Create(SCardScope.System);
                var started = false;
                try
                {
                    monitor.CardRemoved += (_, _) => removed();
                    monitor.MonitorException += (_, _) => removed();
                    monitor.Start(reader);
                    started = true;
                    return monitor;
                }
                finally
                {
                    if (!started)
                        monitor.Dispose();
                }
            },
            "Credential.Monitor",
            "The reader could not monitor credential removal."
        );
}
