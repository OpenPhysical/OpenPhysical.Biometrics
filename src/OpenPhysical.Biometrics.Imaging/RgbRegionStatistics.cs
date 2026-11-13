// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

using CSharpFunctionalExtensions;

/// <summary>Decoded RGB evidence for a mask, with absent statistics for empty samples.</summary>
/// <param name="PixelCount">Number of selected pixels.</param>
/// <param name="Red">Red-channel evidence, or none for an empty region.</param>
/// <param name="Green">Green-channel evidence, or none for an empty region.</param>
/// <param name="Blue">Blue-channel evidence, or none for an empty region.</param>
/// <param name="RedGreenCorrelation">Red/green correlation, absent when either variance is zero.</param>
/// <param name="RedBlueCorrelation">Red/blue correlation, absent when either variance is zero.</param>
/// <param name="GreenBlueCorrelation">Green/blue correlation, absent when either variance is zero.</param>
public sealed record RgbRegionStatistics(
    long PixelCount,
    Maybe<PixelChannelStatistics> Red,
    Maybe<PixelChannelStatistics> Green,
    Maybe<PixelChannelStatistics> Blue,
    Maybe<double> RedGreenCorrelation,
    Maybe<double> RedBlueCorrelation,
    Maybe<double> GreenBlueCorrelation
);
