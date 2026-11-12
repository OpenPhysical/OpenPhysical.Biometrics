// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;

internal sealed record TemplateManifest(
    int Version,
    string TemplateSha256,
    ImmutableArray<TemplateManifestImage> Images
)
{
    /// <summary>Gets the explicitly supplied subject, or an empty array for an unassigned template.</summary>
    public ImmutableArray<CapturePerson> Subjects { get; init; } = [];
}
