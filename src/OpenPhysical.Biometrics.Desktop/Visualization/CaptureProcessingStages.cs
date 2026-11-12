// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class CaptureProcessingStages
{
    internal static Result<Maybe<FingerprintScene>, DesktopError> Scene(
        CaptureProcessingDetailsSource source,
        string stage
    )
    {
        if (stage == "Original")
            return SceneFactory.Frame(source.Original, []).Map(Maybe.From);
        if (stage == "Processing image")
            return source.Processing.Match(
                artifact => SceneFactory.Processed(artifact, []).Map(Maybe.From),
                () =>
                    Result.Success<Maybe<FingerprintScene>, DesktopError>(
                        Maybe<FingerprintScene>.None
                    )
            );
        var mask = Mask(source, stage);
        if (mask.HasNoValue || mask.Value.IsDefaultOrEmpty)
            return Maybe<FingerprintScene>.None;
        var pixels = mask
            .Value.Select(value => value == 0 ? (byte)255 : (byte)0)
            .ToImmutableArray();
        return DesktopBitmap
            .Create(pixels, source.Original.Width, source.Original.Height)
            .Map(bitmap =>
                Maybe.From(
                    new FingerprintScene(
                        source.Original.Width,
                        source.Original.Height,
                        [],
                        Maybe.From(bitmap)
                    )
                    {
                        GrayPixels = pixels,
                    }
                )
            );
    }

    private static Maybe<ImmutableArray<byte>> Mask(
        CaptureProcessingDetailsSource source,
        string stage
    ) =>
        stage switch
        {
            "Measured contact" => source.Evidence.Map(value => value.Contour.ContactMask),
            "Filled holes" => source.Evidence.Map(value => value.Contour.FilledHoleMask),
            "Feature area" => source.Evidence.Map(value => value.Contour.FeatureMask),
            "Selected ridge support" => source.Evidence.Map(value => value.Contour.SupportMask),
            "Uncertain area" => source.Evidence.Map(value => value.Contour.UncertainMask),
            "Image context" => source.Evidence.Map(value => value.Contour.ContextMask),
            "Guarded context" => source.Evidence.Map(value =>
                value.ProcessingRegion.GuardedContextMask
            ),
            "Eligible features" => source.Processing.Map(value => value.Mask.Inner),
            "Candidate mask" => source.Diagnostics.Map(value => value.CandidateMask),
            "Ridge support" => source.Diagnostics.Map(value => value.RidgeSupportMask),
            "Rejected area" => source.Diagnostics.Map(value => value.RejectedMask),
            _ => Maybe<ImmutableArray<byte>>.None,
        };
}
