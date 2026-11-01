// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Defines the stable error codes returned by PIV operations.</summary>
public static class PivErrorCodes
{
    /// <summary>The FASC-N does not contain 25 bytes.</summary>
    public const string InvalidFascN = "InvalidFascN";

    /// <summary>The card UUID is empty.</summary>
    public const string InvalidCardUuid = "InvalidCardUuid";

    /// <summary>The signed content is shorter than a CBEFF header.</summary>
    public const string SignedContentLength = "SignedContentLength";

    /// <summary>The CBEFF header and expected identity carry different FASC-N values.</summary>
    public const string HeaderIdentityMismatch = "HeaderIdentityMismatch";

    /// <summary>The signature block length differs from the signed header.</summary>
    public const string SignatureLength = "SignatureLength";

    /// <summary>The CBEFF record could not be serialized for signing.</summary>
    public const string InvalidRecord = "InvalidRecord";

    /// <summary>The external signing service failed.</summary>
    public const string SigningProviderFailed = "SigningProviderFailed";

    /// <summary>The external verification service failed.</summary>
    public const string VerificationProviderFailed = "VerificationProviderFailed";

    /// <summary>Cancellation was requested before the operation completed.</summary>
    public const string Cancelled = "Cancelled";
}
