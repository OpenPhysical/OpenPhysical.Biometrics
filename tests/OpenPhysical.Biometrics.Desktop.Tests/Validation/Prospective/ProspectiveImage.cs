// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;

internal sealed record ProspectiveImage(
    string Id,
    string PersonId,
    string FingerId,
    ImmutableArray<ProspectiveRepresentation> Representations
)
{
    internal ProspectiveRepresentation For(ProspectiveArm arm) =>
        Representations.Single(value => value.Arm == arm);
}
