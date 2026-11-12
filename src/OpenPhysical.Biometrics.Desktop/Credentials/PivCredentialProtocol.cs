// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class PivCredentialProtocol
{
    internal static Result<TemplateDocument, DesktopError> Read(
        ICredentialCard card,
        ReadOnlyMemory<char> pin,
        CancellationToken token
    )
    {
        if (!ValidPin(pin.Span))
            return new DesktopError("Credential.Pin", "Enter the card PIN using 6 to 8 digits.");
        return CredentialExchange
            .Read(card, Convert.FromHexString("00A404000BA00000030800001000010000"), token)
            .Bind(_ => Verify(card, pin, token))
            .Bind(_ =>
                CredentialExchange.Read(
                    card,
                    Convert.FromHexString("00CB3FFF055C035FC10300"),
                    token
                )
            )
            .Bind(CredentialBiometricObject.Read)
            .Bind(TemplateRecordReader.Parse)
            .Map(record => new TemplateDocument(record, []));
    }

    private static Result<bool, DesktopError> Verify(
        ICredentialCard card,
        ReadOnlyMemory<char> pin,
        CancellationToken token
    )
    {
        var command = new byte[] { 0, 0x20, 0, 0x80, 8, 255, 255, 255, 255, 255, 255, 255, 255 };
        for (var i = 0; i < pin.Length; i++)
            command[5 + i] = (byte)pin.Span[i];
        try
        {
            return card.Transmit(command, token)
                .Bind(CredentialResponse.Parse)
                .Bind(response => response.Success().Map(() => true));
        }
        finally
        {
            Array.Clear(command);
        }
    }

    private static bool ValidPin(ReadOnlySpan<char> pin)
    {
        if (pin.Length is < 6 or > 8)
            return false;
        foreach (var character in pin)
            if (character is < '0' or > '9')
                return false;
        return true;
    }
}
