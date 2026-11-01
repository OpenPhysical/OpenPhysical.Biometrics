// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

/// <summary>Contains facial metadata encoded by INCITS 385-2004.</summary>
/// <param name="Gender">Encoded gender value.</param>
/// <param name="EyeColor">Encoded eye-colour value.</param>
/// <param name="HairColor">Encoded hair-colour value.</param>
/// <param name="FeatureMask">24-bit feature mask.</param>
/// <param name="Expression">Encoded expression value.</param>
/// <param name="PoseAngle">Three packed pose-angle bytes, retained in wire order.</param>
/// <param name="PoseUncertainty">Three packed pose-uncertainty bytes, retained in wire order.</param>
public sealed record FaceInformation(
    byte Gender,
    byte EyeColor,
    byte HairColor,
    uint FeatureMask,
    ushort Expression,
    uint PoseAngle,
    uint PoseUncertainty
);
