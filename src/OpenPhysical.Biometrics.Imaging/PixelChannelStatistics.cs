// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Native eight-bit channel statistics; entropy uses sixteen fixed bins.</summary>
/// <param name="Mean">Arithmetic sample mean.</param>
/// <param name="Variance">Population variance.</param>
/// <param name="EntropyBits">Sixteen-bin entropy in bits.</param>
/// <param name="MeanNeighborGradient">Mean absolute difference between selected adjacent samples.</param>
/// <param name="EdgeDensity">Fraction of selected adjacent differences exceeding the explicit threshold.</param>
public sealed record PixelChannelStatistics(
    double Mean,
    double Variance,
    double EntropyBits,
    double MeanNeighborGradient,
    double EdgeDensity
);
