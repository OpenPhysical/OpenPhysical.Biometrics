// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;

/// <summary>Evidence of an explicit background edit, separate from original-image measurements.</summary>
/// <param name="SourceSha256">Original acquisition identity.</param>
/// <param name="OriginalPortraitSha256">RGB digest before background editing.</param>
/// <param name="PortraitSha256">RGB digest after editing.</param>
/// <param name="MaskSha256">Digest of the native person-confidence samples.</param>
/// <param name="ModelSha256">Pinned segmentation model identity.</param>
/// <param name="MaskPolicyId">Source-coordinate segmentation sampling convention.</param>
/// <param name="Policy">Exact replacement and preservation parameters.</param>
/// <param name="Fill">Median original-background RGB and sample count, absent when no removal region exists.</param>
/// <param name="ReplacedPixelCount">Pixels with nonzero replacement opacity.</param>
/// <param name="OriginalBackground">Original samples in the fully replaced region.</param>
/// <param name="ResultBackground">Edited samples in that same region, not a whole-image compliance claim.</param>
public sealed record PortraitBackgroundEvidence(
    string SourceSha256,
    string OriginalPortraitSha256,
    string PortraitSha256,
    string MaskSha256,
    string ModelSha256,
    string MaskPolicyId,
    PortraitBackgroundPolicy Policy,
    Maybe<PortraitBackgroundColor> Fill,
    int ReplacedPixelCount,
    RgbRegionStatistics OriginalBackground,
    RgbRegionStatistics ResultBackground
);
