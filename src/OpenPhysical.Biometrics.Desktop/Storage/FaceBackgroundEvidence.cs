// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal static class FaceBackgroundEvidence
{
    internal static JsonElement Create(Maybe<PortraitBackgroundEvidence> evidence) =>
        evidence.Match(
            value =>
                JsonSerializer.SerializeToElement(
                    new
                    {
                        Requested = true,
                        Applied = value.ReplacedPixelCount > 0,
                        value.SourceSha256,
                        value.OriginalPortraitSha256,
                        value.PortraitSha256,
                        value.MaskSha256,
                        value.ModelSha256,
                        value.MaskPolicyId,
                        value.Policy,
                        Fill = FaceQualityEvidence.Values(value.Fill),
                        value.ReplacedPixelCount,
                        OriginalBackground = FaceQualityEvidence.Region(value.OriginalBackground),
                        ResultBackground = FaceQualityEvidence.Region(value.ResultBackground),
                    }
                ),
            () => JsonSerializer.SerializeToElement(new { Applied = false })
        );
}
