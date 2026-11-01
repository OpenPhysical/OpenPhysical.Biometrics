// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

using System.Collections.Immutable;

/// <summary>Contains one facial image block and its immutable metadata.</summary>
/// <param name="Information">Facial metadata.</param>
/// <param name="FeaturePoints">Feature points in wire order.</param>
/// <param name="ImageInformation">Encoded-image metadata.</param>
/// <param name="ImageData">Compressed image bytes, copied from their bounded block during parsing.</param>
public sealed record FaceImage(
    FaceInformation Information,
    ImmutableArray<FaceFeaturePoint> FeaturePoints,
    FaceImageInformation ImageInformation,
    ImmutableArray<byte> ImageData
);
