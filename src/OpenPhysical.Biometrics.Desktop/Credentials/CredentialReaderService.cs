// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Reads PIV fingerprint references through an owned reader transport.</summary>
/// <param name="transport">Owned hardware reader transport.</param>
public sealed class CredentialReaderService(ICredentialTransport transport)
{
    /// <summary>Enumerates connected readers without retaining native resources.</summary>
    /// <param name="token">Cancellation checked before enumeration.</param>
    /// <returns>Reader names or a resource-manager failure.</returns>
    public Task<Result<ImmutableArray<string>, DesktopError>> ReadersAsync(
        CancellationToken token
    ) =>
        Task.Run<Result<ImmutableArray<string>, DesktopError>>(() =>
            token.IsCancellationRequested
                ? new DesktopError("Cancelled", "Reader enumeration was cancelled.")
                : transport.Readers()
        );

    /// <summary>Reads a PIV fingerprint reference using one owned card transaction.</summary>
    /// <param name="reader">Selected reader name.</param>
    /// <param name="pin">Caller-owned PIN memory, valid until this task completes.</param>
    /// <param name="token">Cancellation checked before card commands.</param>
    /// <returns>The complete reference document or a transport, PIN or format failure.</returns>
    public Task<Result<TemplateDocument, DesktopError>> ReadAsync(
        string reader,
        ReadOnlyMemory<char> pin,
        CancellationToken token
    ) =>
        Task.Run(
            () => transport.UseReader(reader, card => PivCredentialProtocol.Read(card, pin, token)),
            CancellationToken.None
        );

    /// <summary>Owns a removal monitor for a loaded card reference.</summary>
    /// <param name="reader">Reader containing the reference card.</param>
    /// <param name="removed">Invalidates a reference when the card is removed.</param>
    /// <returns>The owned monitor or a monitoring failure.</returns>
    public Result<IDisposable, DesktopError> WatchRemoval(
        string reader,
        Func<UnitResult<DesktopError>> removed
    ) => transport.WatchRemoval(reader, removed);
}
