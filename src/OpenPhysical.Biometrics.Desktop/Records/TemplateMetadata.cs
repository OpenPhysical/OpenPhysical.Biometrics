// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Product and acquisition identifiers for an unsigned fingerprint record.</summary>
/// <param name="ProductOwner">The nonzero registered or supplied product owner identifier.</param>
/// <param name="ProductType">The product type identifier.</param>
/// <param name="EquipmentId">The 12-bit acquisition equipment identifier.</param>
public sealed record TemplateMetadata(ushort ProductOwner, ushort ProductType, ushort EquipmentId);
