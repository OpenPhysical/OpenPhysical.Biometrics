// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Owns native reader resources separately from the card protocol.</summary>
public interface ICredentialTransport
{
    /// <summary>Enumerates connected PC/SC readers.</summary>
    /// <returns>Reader names or a resource-manager failure.</returns>
    Result<ImmutableArray<string>, DesktopError> Readers();

    /// <summary>Runs one operation within an owned card connection and transaction.</summary>
    /// <param name="reader">Selected reader name.</param>
    /// <param name="operation">Read-only PIV operation.</param>
    /// <returns>The loaded template or a connection/protocol failure.</returns>
    Result<TemplateDocument, DesktopError> UseReader(
        string reader,
        Func<ICredentialCard, Result<TemplateDocument, DesktopError>> operation
    );

    /// <summary>Monitors removal or reader failure with its own native context.</summary>
    /// <param name="reader">Reader containing the reference card.</param>
    /// <param name="removed">Callback invalidating the reference.</param>
    /// <returns>An owned monitor or a monitoring failure.</returns>
    Result<IDisposable, DesktopError> WatchRemoval(
        string reader,
        Func<UnitResult<DesktopError>> removed
    );
}
