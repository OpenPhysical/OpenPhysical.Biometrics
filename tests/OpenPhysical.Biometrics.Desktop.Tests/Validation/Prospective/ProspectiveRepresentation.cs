// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;

internal sealed record ProspectiveRepresentation(
    ProspectiveArm Arm,
    [property: JsonIgnore] Maybe<FingerprintTemplate> Template,
    [property: JsonIgnore] Maybe<DesktopError> Rejection
)
{
    [JsonIgnore]
    public Maybe<ProspectiveImageEvidence> Evidence { get; init; }
    public bool Available => Template.HasValue && Rejection.HasNoValue;
    public DesktopError[] Diagnostics => Rejection.Match(value => new[] { value }, () => []);
    public ProspectiveImageEvidence[] Assessment =>
        Evidence.Match(value => new[] { value }, () => []);
}
