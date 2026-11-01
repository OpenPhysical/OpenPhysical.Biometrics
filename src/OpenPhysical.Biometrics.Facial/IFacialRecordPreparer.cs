// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>Prepares captured sRGB pixels as a PIV facial image record.</summary>
public interface IFacialRecordPreparer
{
    /// <summary>Encodes an attended capture as JPEG 2000, preserving its original geometry.</summary>
    /// <param name="srgbPixels">Tightly packed red, green, blue bytes in row-major order, starting at the top left.</param>
    /// <param name="width">Original width in pixels.</param>
    /// <param name="height">Original height in pixels.</param>
    /// <param name="acquisition">Camera metadata and operator-confirmed acquisition findings.</param>
    /// <returns>A canonical facial record or an expected acquisition/profile outcome; a failure reports invalid dimensions, pixels, or encoder faults.</returns>
    Result<FacialPreparationResult, FacialError> Prepare(
        ReadOnlySpan<byte> srgbPixels,
        int width,
        int height,
        FacialAcquisitionMetadata acquisition
    );
}
