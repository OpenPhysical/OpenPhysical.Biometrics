// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

/// <summary>The reader communication edge used by the PIV fingerprint protocol.</summary>
public interface ICredentialCard
{
    /// <summary>Sends one command and returns its complete response and status bytes.</summary>
    /// <param name="command">The explicit APDU to transmit.</param>
    /// <param name="cancellationToken">Cancellation checked before transmission.</param>
    /// <returns>Response bytes or a transport failure.</returns>
    Result<byte[], DesktopError> Transmit(byte[] command, CancellationToken cancellationToken);
}
