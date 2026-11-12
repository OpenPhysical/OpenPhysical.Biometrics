// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Bounded image-admission evidence, without an anatomical or liveness assertion.</summary>
public enum Fs50WholeContactDecision
{
    /// <summary>No compatible reference or previously qualified contour is available.</summary>
    Unavailable,

    /// <summary>No valid contact was observed; rejected observations are not removal.</summary>
    NoContact,

    /// <summary>A compatible empty reference spatially corroborates this whole contour.</summary>
    QualifiedReference,

    /// <summary>The current contour remains within an anchored previously qualified presentation.</summary>
    QualifiedGeometry,

    /// <summary>A substantial exterior region was directly observed to remain static.</summary>
    StaticExterior,

    /// <summary>A substantial exterior region lacks enough compatible corroboration.</summary>
    UnresolvedExterior,

    /// <summary>A light coherent candidate is spatially corroborated for acquisition correction only.</summary>
    ExposureReference,

    /// <summary>A light candidate stays within a fixed spatial anchor, permitting correction only.</summary>
    ExposureGeometry,

    /// <summary>Original single-contact ridge evidence permits capture; background comparison is unavailable.</summary>
    SpatialCapture,

    /// <summary>A light coherent contact permits correction only; background comparison is unavailable.</summary>
    SpatialExposure,
}
