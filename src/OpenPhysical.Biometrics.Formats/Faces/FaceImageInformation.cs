// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

/// <summary>Describes the encoded face image metadata.</summary>
/// <param name="FaceImageType">Encoded facial image type.</param>
/// <param name="ImageDataType">Zero for JPEG or one for JPEG 2000.</param>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="ColorSpace">Encoded colour-space value.</param>
/// <param name="SourceType">Encoded capture-source value.</param>
/// <param name="DeviceType">Capture-device identifier.</param>
/// <param name="Quality">The complete unsigned 16-bit quality field, including standard sentinel values.</param>
public sealed record FaceImageInformation(
    byte FaceImageType,
    byte ImageDataType,
    ushort Width,
    ushort Height,
    byte ColorSpace,
    byte SourceType,
    ushort DeviceType,
    ushort Quality
);
