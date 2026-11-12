// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A saved artifact belonging to a person enrollment section.</summary>
/// <param name="SectionId">Stable owning section identifier.</param>
/// <param name="MediaType">Artifact content type.</param>
/// <param name="Path">Persistent local artifact path.</param>
public sealed record EnrollmentArtifact(string SectionId, string MediaType, string Path);
