// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>A decoded fingerprint record and verified images supplied with a Studio package.</summary>
/// <param name="Record">The decoded ANSI fingerprint minutiae record.</param>
/// <param name="Images">Verified images in record view order, empty for standalone templates.</param>
public sealed record TemplateDocument(
    FingerprintRecord Record,
    ImmutableArray<TemplateImage> Images
)
{
    /// <summary>Gets the user-provided local subject association when saved with the template.</summary>
    public ImmutableArray<CapturePerson> Subjects { get; init; } = [];
}
