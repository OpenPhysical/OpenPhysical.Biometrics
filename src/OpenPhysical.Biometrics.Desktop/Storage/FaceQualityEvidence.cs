// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Imaging;

internal static class FaceQualityEvidence
{
    internal static JsonElement Mouth(PortraitPreparationResult portrait) =>
        JsonSerializer.SerializeToElement(
            new { Policy = portrait.MouthPolicy, Measurement = Values(portrait.MouthMeasurement) }
        );

    internal static JsonElement Create(PortraitQualityMeasurements quality) =>
        JsonSerializer.SerializeToElement(
            new
            {
                quality.SourceSha256,
                quality.PortraitSha256,
                quality.PolicyId,
                quality.Coordinates,
                MaskedLaplacianVariance = Values(quality.MaskedLaplacianVariance),
                Backdrop = new
                {
                    quality.Backdrop.EstimatedCandidate,
                    quality.Backdrop.CandidateFraction,
                    quality.Backdrop.ColorConsistency,
                    quality.Backdrop.MaskSha256,
                    quality.Backdrop.Policy,
                    Statistics = Region(quality.Backdrop.Statistics),
                    Face = Region(quality.Backdrop.Face),
                    ProtectedForeground = Region(quality.Backdrop.ProtectedForeground),
                    Unclassified = Region(quality.Backdrop.Unclassified),
                },
                quality.Findings,
            }
        );

    internal static T[] Values<T>(Maybe<T> value) =>
        value.Match(item => new[] { item }, Array.Empty<T>);

    internal static JsonElement Region(RgbRegionStatistics value) =>
        JsonSerializer.SerializeToElement(
            new
            {
                value.PixelCount,
                Red = Values(value.Red),
                Green = Values(value.Green),
                Blue = Values(value.Blue),
                RedGreenCorrelation = Values(value.RedGreenCorrelation),
                RedBlueCorrelation = Values(value.RedBlueCorrelation),
                GreenBlueCorrelation = Values(value.GreenBlueCorrelation),
            }
        );
}
