// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using PCSC;

internal sealed class PcscCredentialCard(ICardReader card) : ICredentialCard
{
    public Result<byte[], DesktopError> Transmit(
        byte[] command,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
            return new DesktopError("Cancelled", "Credential reading was cancelled.");
        return DesktopEdge.Try(
            () =>
            {
                var response = new byte[65538];
                var length = card.Transmit(command, response);
                return response.AsSpan(0, length).ToArray();
            },
            "Credential.Transport",
            "Communication with the credential failed."
        );
    }
}
