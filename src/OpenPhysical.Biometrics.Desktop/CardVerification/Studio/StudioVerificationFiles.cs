// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Server.Contracts;
using OpenPhysical.Workstation.Application.Pairing;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationFiles
{
    internal static async Task<Result<PairingRequest, DesktopError>> PairingAsync(
        DesktopFilePicker picker,
        string origin,
        CancellationToken token
    )
    {
        if (
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
        )
            return new DesktopError(
                "Verification.Server",
                "Enter the HTTPS server address from your administrator."
            );
        var file = await picker.OpenVerificationInvitationAsync();
        if (file.IsFailure)
            return file.Error;
        var read = await ReadAsync(file.Value, 32768, token);
        if (read.IsFailure)
            return read.Error;
        try
        {
            return ParseInvitation(read.Value, uri);
        }
        finally
        {
            Array.Clear(read.Value);
        }
    }

    private static Result<PairingRequest, DesktopError> ParseInvitation(byte[] bytes, Uri origin)
    {
        var parsed = DesktopEdge.Try(
            () =>
                JsonSerializer.Deserialize<SchemaEnvelope<PairingInvitationDelivery>>(
                    bytes,
                    ServerJson.CreateOptions()
                ),
            "Verification.Invitation",
            "Select a valid server invitation."
        );
        return parsed.IsSuccess && parsed.Value is { SchemaVersion: 1 } delivery
            ? new PairingRequest(
                origin,
                delivery.Value.Invitation,
                delivery.Value.Token,
                Environment.MachineName
            )
            : new DesktopError("Verification.Invitation", "Select a valid server invitation.");
    }

    internal static async Task<Result<byte[], DesktopError>> ReadAsync(
        string path,
        int limit,
        CancellationToken token
    )
    {
        var read = await DesktopEdge.TryAsync(
            () => ReadOwnedAsync(path, limit, token),
            "Verification.File",
            "This file could not be opened."
        );
        return read.IsSuccess
            ? read.Value
            : new DesktopError(read.Error.Code, "This file could not be opened.");
    }

    private static async Task<Result<byte[], DesktopError>> ReadOwnedAsync(
        string path,
        int limit,
        CancellationToken token
    )
    {
        await using var file = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous
        );
        if (file.Length is <= 0 || file.Length > limit)
            return new DesktopError("Verification.FileSize", "This file is empty or too large.");
        return await ReadExactlyAsync(file, new byte[(int)file.Length], token);
    }

    private static async Task<Result<byte[], DesktopError>> ReadExactlyAsync(
        FileStream file,
        byte[] bytes,
        CancellationToken token
    )
    {
        var delivered = false;
        try
        {
            await file.ReadExactlyAsync(bytes, token);
            if (file.ReadByte() != -1)
                return new DesktopError(
                    "Verification.FileSize",
                    "This file changed while opening."
                );
            delivered = true;
            return Result.Success<byte[], DesktopError>(bytes);
        }
        finally
        {
            if (!delivered)
                Array.Clear(bytes);
        }
    }

    internal static Result<ImmutableArray<byte>, DesktopError> Certificate(byte[] bytes)
    {
        var parsed = DesktopEdge.Try(
            () => new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(bytes),
            "Verification.Certificate",
            "Select a valid certificate."
        );
        return parsed.IsSuccess && parsed.Value is not null
            ? parsed.Value.GetEncoded().ToImmutableArray()
            : new DesktopError("Verification.Certificate", "Select a valid certificate.");
    }
}
