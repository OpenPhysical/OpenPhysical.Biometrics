// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

/// <summary>Identifies the extractor and the record geometry used during PIV issuance.</summary>
public sealed class PivFingerprintRecordMetadata
{
    private PivFingerprintRecordMetadata(
        ushort productOwner,
        ushort productType,
        ushort equipmentId,
        ushort width,
        ushort height,
        bool equipmentAppendixFCertified
    )
    {
        ProductOwner = productOwner;
        ProductType = productType;
        EquipmentId = equipmentId;
        Width = width;
        Height = height;
        EquipmentAppendixFCertified = equipmentAppendixFCertified;
    }

    /// <summary>Gets the registered extractor vendor identifier.</summary>
    public ushort ProductOwner { get; }

    /// <summary>Gets the registered extractor product/version identifier.</summary>
    public ushort ProductType { get; }

    /// <summary>Gets the registered capture device identifier.</summary>
    public ushort EquipmentId { get; }

    /// <summary>Gets the largest original image width in pixels.</summary>
    public ushort Width { get; }

    /// <summary>Gets the largest original image height in pixels.</summary>
    public ushort Height { get; }

    /// <summary>Gets the acquisition equipment certification supplied by the application.</summary>
    public bool EquipmentAppendixFCertified { get; }

    /// <summary>Creates the registered identifiers and explicit original-image geometry.</summary>
    /// <param name="productOwner">Registered extractor vendor identifier, greater than zero.</param>
    /// <param name="productType">Registered extractor product/version identifier, greater than zero.</param>
    /// <param name="equipmentId">Registered capture device identifier, from 1 through 4095.</param>
    /// <param name="width">Largest original image width in pixels.</param>
    /// <param name="height">Largest original image height in pixels.</param>
    /// <param name="equipmentAppendixFCertified">Whether the acquisition equipment carries Appendix F certification.</param>
    /// <returns>The metadata, or an error when an identifier or dimension is invalid.</returns>
    public static Result<PivFingerprintRecordMetadata, FingerprintError> Create(
        ushort productOwner,
        ushort productType,
        ushort equipmentId,
        ushort width,
        ushort height,
        bool equipmentAppendixFCertified
    ) =>
        FingerprintChecks
            .Require(
                productOwner != 0 && productType != 0,
                FingerprintErrorCodes.InvalidProduct,
                "Registered product identifiers must be greater than zero."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    equipmentId is >= 1 and <= 4095,
                    FingerprintErrorCodes.InvalidEquipment,
                    "The equipment identifier must be from 1 through 4095."
                )
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    width is >= 1 and <= 2048 && height is >= 1 and <= 2048,
                    FingerprintErrorCodes.RecordDimensions,
                    "Record dimensions must be from 1 through 2048 pixels."
                )
            )
            .Map(() =>
                new PivFingerprintRecordMetadata(
                    productOwner,
                    productType,
                    equipmentId,
                    width,
                    height,
                    equipmentAppendixFCertified
                )
            );
}
