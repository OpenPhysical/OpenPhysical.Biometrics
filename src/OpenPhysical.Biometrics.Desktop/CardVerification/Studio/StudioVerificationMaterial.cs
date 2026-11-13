// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class StudioVerificationMaterial(DesktopFilePicker picker)
{
    private byte[] privacyKey = [];
    internal ImmutableArray<ImmutableArray<byte>> Issuers { get; private set; } = [];
    internal ImmutableArray<ImmutableArray<byte>> Signers { get; private set; } = [];
    internal string Summary =>
        $"{Issuers.Length} issuer and {Signers.Length} content signer certificates. "
        + (privacyKey.Length > 0 ? "TWIC privacy key loaded." : "No TWIC privacy key loaded.");

    internal async Task<UnitResult<DesktopError>> ImportCertificateAsync(
        bool signer,
        CancellationToken token
    )
    {
        var current = signer ? Signers : Issuers;
        if (current.Length >= 8)
            return new DesktopError(
                "Verification.CertificateLimit",
                "At most eight candidate certificates can be added."
            );
        var path = await picker.OpenVerificationCertificateAsync();
        if (path.IsFailure)
            return path.Error;
        var read = await StudioVerificationFiles.ReadAsync(path.Value, 65536, token);
        if (read.IsFailure)
            return read.Error;
        try
        {
            var parsed = StudioVerificationFiles.Certificate(read.Value);
            if (parsed.IsFailure)
                return parsed.Error;
            if (signer)
                Signers = Signers.Add(parsed.Value);
            else
                Issuers = Issuers.Add(parsed.Value);
            return UnitResult.Success<DesktopError>();
        }
        finally
        {
            Array.Clear(read.Value);
        }
    }

    internal async Task<UnitResult<DesktopError>> ImportPrivacyKeyAsync(CancellationToken token)
    {
        ClearKey();
        var path = await picker.OpenTwicPrivacyKeyAsync();
        if (path.IsFailure)
            return path.Error;
        var read = await StudioVerificationFiles.ReadAsync(path.Value, 1024, token);
        if (read.IsFailure)
            return read.Error;
        privacyKey = read.Value;
        return UnitResult.Success<DesktopError>();
    }

    internal byte[] TakePrivacyKey()
    {
        var result = privacyKey;
        privacyKey = [];
        return result;
    }

    internal UnitResult<DesktopError> Clear()
    {
        ClearKey();
        Issuers = [];
        Signers = [];
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> ClearKey()
    {
        Array.Clear(privacyKey);
        privacyKey = [];
        return UnitResult.Success<DesktopError>();
    }
}
