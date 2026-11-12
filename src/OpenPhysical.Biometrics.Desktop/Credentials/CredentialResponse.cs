// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed record CredentialResponse(byte[] Data, ushort Status)
{
    internal static Result<CredentialResponse, DesktopError> Parse(byte[] response) =>
        response.Length >= 2
            ? new CredentialResponse(
                response.AsSpan(0, response.Length - 2).ToArray(),
                (ushort)((response[^2] << 8) | response[^1])
            )
            : new DesktopError(
                "Credential.Response",
                "The credential returned an incomplete status."
            );

    internal UnitResult<DesktopError> Success() =>
        Status == 0x9000
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(
                Status == 0x6983
                    ? new DesktopError("Credential.PinBlocked", "The card PIN is blocked.")
                : (Status & 0xFFF0) == 0x63C0
                    ? new DesktopError(
                        "Credential.PinRejected",
                        $"The PIN was not accepted. {Status & 15} attempts remain."
                    )
                : new DesktopError(
                    "Credential.Status",
                    $"The credential returned status {Status:X4}."
                )
            );
}
