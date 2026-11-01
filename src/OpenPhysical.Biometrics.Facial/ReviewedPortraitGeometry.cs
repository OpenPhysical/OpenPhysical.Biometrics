// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>Operator-reviewed geometry tied to the exact encoded source.</summary>
/// <param name="Version">Schema version, currently one.</param>
/// <param name="SourceSha256">SHA-256 of the original encoded image.</param>
/// <param name="Width">Width after applying EXIF orientation.</param>
/// <param name="Height">Height after applying EXIF orientation.</param>
/// <param name="FaceIndex">Selected detection in stable display order.</param>
/// <param name="Crown">Reviewed top of the hair, when supplied.</param>
/// <param name="Chin">Reviewed bottom of the chin, or none to use the mesh.</param>
/// <param name="ViewerLeftEar">Reviewed left ear attachment, when supplied.</param>
/// <param name="ViewerRightEar">Reviewed right ear attachment, when supplied.</param>
public sealed record ReviewedPortraitGeometry(
    int Version,
    string SourceSha256,
    int Width,
    int Height,
    int FaceIndex,
    Maybe<PortraitPoint> Crown = default,
    Maybe<PortraitPoint> Chin = default,
    Maybe<PortraitPoint> ViewerLeftEar = default,
    Maybe<PortraitPoint> ViewerRightEar = default
);
