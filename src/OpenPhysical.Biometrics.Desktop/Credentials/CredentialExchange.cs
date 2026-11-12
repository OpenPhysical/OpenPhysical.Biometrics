// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class CredentialExchange
{
    internal static Result<byte[], DesktopError> Read(
        ICredentialCard card,
        byte[] command,
        CancellationToken token
    )
    {
        var accumulated = new List<byte>();
        var next = command;
        for (var count = 0; count < 128; count++)
        {
            if (token.IsCancellationRequested)
                return new DesktopError("Cancelled", "Credential reading was cancelled.");
            var result = card.Transmit(next, token).Bind(CredentialResponse.Parse);
            if (result.IsFailure)
                return result.Error;
            var response = result.Value;
            if ((response.Status >> 8) == 0x6C)
            {
                next = next.ToArray();
                next[^1] = (byte)response.Status;
                continue;
            }
            accumulated.AddRange(response.Data);
            if (accumulated.Count > 32768)
                return new DesktopError(
                    "Credential.Size",
                    "The fingerprint response exceeds its supported size."
                );
            if ((response.Status >> 8) == 0x61)
            {
                next = [0, 0xC0, 0, 0, (byte)response.Status];
                continue;
            }
            return response.Success().Map(() => accumulated.ToArray());
        }
        return new DesktopError(
            "Credential.Chaining",
            "The credential response did not complete within its chaining limit."
        );
    }
}
