// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Accepted unsigned codec objects bound to one person and privacy generation.</summary>
/// <param name="Package">Immutable unsigned artifacts and their existing acquisition evidence.</param>
/// <param name="PlanId">The exact accepted capture plan.</param>
/// <param name="PrivacyRevision">The privacy generation at preparation.</param>
public sealed record AcceptedBiometricSnapshot(
    BiometricPackage Package,
    string PlanId,
    long PrivacyRevision
);
