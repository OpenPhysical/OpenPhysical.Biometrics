// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Geometry counts without assigning image quality or physical-contact certainty.</summary>
/// <param name="SelectedContactArea">Original selected texture area C.</param>
/// <param name="FilledHoleArea">Inferred enclosed area H.</param>
/// <param name="FeatureArea">Full feature-admission area F.</param>
/// <param name="MeasuredSupportArea">Measured selected ridge-support area S.</param>
/// <param name="ContextArea">Image-context area E.</param>
/// <param name="UncertainArea">Feature area without measured support F minus S.</param>
/// <param name="ContextHullVertexCount">Number of full-contact convex-envelope vertices.</param>
public sealed record Fs50ContactContourStatistics(
    int SelectedContactArea,
    int FilledHoleArea,
    int FeatureArea,
    int MeasuredSupportArea,
    int ContextArea,
    int UncertainArea,
    int ContextHullVertexCount
);
