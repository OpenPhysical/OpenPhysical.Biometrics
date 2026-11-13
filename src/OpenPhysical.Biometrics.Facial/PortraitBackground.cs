// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Buffers.Binary;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;

/// <summary>Replaces only confidently identified background outside protected person boundaries.</summary>
public static class PortraitBackground
{
    /// <summary>Composites one frozen crop without changing geometry, source pixels or capture findings.</summary>
    /// <param name="analysis">Original analysis enriched with its matching cached person mask.</param>
    /// <param name="portrait">Unedited source-supported crop.</param>
    /// <param name="policy">Application editing and protection parameters.</param>
    /// <returns>New immutable pixels and editing evidence, or a typed mask/source failure.</returns>
    public static Result<PortraitPreparationResult, FacialError> Apply(
        PortraitAnalysis analysis,
        PortraitPreparationResult portrait,
        PortraitBackgroundPolicy policy
    )
    {
        if (!policy.Enabled || portrait.RgbPixels.IsEmpty)
            return portrait;
        if (
            !PortraitInspectionData.IsUsable(portrait)
            || portrait.Faces.Length != 1
            || analysis.PersonMask.HasNoValue
        )
            return Invalid();
        var mask = analysis.PersonMask.Value;
        if (
            mask.SourceSha256 != analysis.SourceSha256
            || mask.SourceSha256 != portrait.SourceSha256
            || mask.SourceWidth != analysis.Width
            || mask.SourceHeight != analysis.Height
            || portrait.SourceWidth != analysis.Width
            || portrait.SourceHeight != analysis.Height
        )
            return Invalid();
        return PortraitBackgroundMatte
            .Create(portrait, mask, policy)
            .Bind(matte => Composite(portrait, mask, matte, policy));
    }

    private static Result<PortraitPreparationResult, FacialError> Composite(
        PortraitPreparationResult portrait,
        PortraitPersonMask mask,
        byte[] matte,
        PortraitBackgroundPolicy policy
    )
    {
        var original = portrait.RgbPixels.ToArray();
        var result = original.ToArray();
        var measured = matte.Select(value => value == 255 ? (byte)1 : (byte)0).ToArray();
        try
        {
            return PortraitBackgroundColor
                .Estimate(original, measured)
                .Bind(fill =>
                {
                    fill.Tap(color => Render(original, result, matte, color));
                    return Evidence(portrait, mask, policy, fill, matte, measured, original, result)
                        .Map(evidence =>
                            portrait with
                            {
                                RgbPixels = ImmutableArray.Create(result),
                                Background = evidence,
                                Findings = Findings(portrait, fill),
                            }
                        );
                });
        }
        finally
        {
            Array.Clear(original);
            Array.Clear(result);
            Array.Clear(matte);
            Array.Clear(measured);
        }
    }

    private static ImmutableArray<PortraitFinding> Findings(
        PortraitPreparationResult portrait,
        Maybe<PortraitBackgroundColor> fill
    ) =>
        fill.HasValue
            ? portrait.Findings
            : portrait.Findings.Add(
                new PortraitFinding(
                    "BackgroundNotRemoved",
                    "No confidently removable background was found; original background retained."
                )
                {
                    TestName = "Background removal",
                }
            );

    private static bool Render(
        byte[] original,
        byte[] result,
        byte[] matte,
        PortraitBackgroundColor color
    )
    {
        var channels = new[] { color.Red, color.Green, color.Blue };
        for (var i = 0; i < matte.Length; i++)
        for (var c = 0; c < 3; c++)
            result[i * 3 + c] = Blend(original[i * 3 + c], channels[c], matte[i]);
        return true;
    }

    private static byte Blend(byte original, byte level, byte opacity) =>
        (byte)((original * (255 - opacity) + level * opacity + 127) / 255);

    private static Result<PortraitBackgroundEvidence, FacialError> Evidence(
        PortraitPreparationResult portrait,
        PortraitPersonMask mask,
        PortraitBackgroundPolicy policy,
        Maybe<PortraitBackgroundColor> fill,
        byte[] matte,
        byte[] measured,
        byte[] original,
        byte[] result
    ) =>
        RgbRegionMeasurements
            .Measure(original, PortraitOutput.Width, PortraitOutput.Height, measured)
            .MapError(error => new FacialError(error.Code, error.Message))
            .Bind(before =>
                RgbRegionMeasurements
                    .Measure(result, PortraitOutput.Width, PortraitOutput.Height, measured)
                    .MapError(error => new FacialError(error.Code, error.Message))
                    .Map(after => new PortraitBackgroundEvidence(
                        portrait.SourceSha256,
                        Sha256.HexDigest(original),
                        Sha256.HexDigest(result),
                        MaskHash(mask),
                        mask.ModelSha256,
                        mask.PolicyId,
                        policy,
                        fill,
                        fill.HasValue ? matte.Count(value => value > 0) : 0,
                        before,
                        after
                    ))
            );

    private static string MaskHash(PortraitPersonMask mask)
    {
        var bytes = new byte[mask.Confidence.Length * 4];
        for (var i = 0; i < mask.Confidence.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), mask.Confidence[i]);
        var hash = Sha256.HexDigest(bytes);
        Array.Clear(bytes);
        return hash;
    }

    private static FacialError Invalid() =>
        new(
            FacialErrorCodes.BackgroundMaskMismatch,
            "Background replacement requires a matching source-bound person mask and usable portrait."
        );
}
