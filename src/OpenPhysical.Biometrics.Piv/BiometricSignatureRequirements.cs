// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Describes the PIV-specific envelope and signed-attribute requirements for an external service.</summary>
public sealed class BiometricSignatureRequirements
{
    /// <summary>Gets the PIV biometric-object content type.</summary>
    public string ContentTypeOid => "2.16.840.1.101.3.6.2";

    /// <summary>Gets the PIV FASC-N signed-attribute identifier.</summary>
    public string FascNAttributeOid => "2.16.840.1.101.3.6.6";

    /// <summary>Gets the entryUUID signed-attribute identifier.</summary>
    public string EntryUuidAttributeOid => "1.3.6.1.1.16.4";

    /// <summary>Gets the PIV signer distinguished-name attribute identifier.</summary>
    public string SignerDnAttributeOid => "2.16.840.1.101.3.6.5";

    /// <summary>Gets the required message-digest signed-attribute identifier.</summary>
    public string MessageDigestAttributeOid => "1.2.840.113549.1.9.4";

    /// <summary>Gets whether CMS identifies the signer by certificate issuer and serial number.</summary>
    public bool UseIssuerAndSerialNumber => true;

    /// <summary>Gets whether the CMS envelope may include a CRL collection.</summary>
    public bool AllowCrls => false;

    /// <summary>Gets the required number of signers.</summary>
    public int SignerCount => 1;

    /// <summary>Gets whether the content is detached.</summary>
    public bool Detached => true;

    /// <summary>Gets the required CMS SignedData version.</summary>
    public int SignedDataVersion => 3;
}
