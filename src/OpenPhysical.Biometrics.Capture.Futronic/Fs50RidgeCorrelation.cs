// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50RidgeCorrelation
{
    internal static double Coefficient(ReadOnlySpan<double> first, ReadOnlySpan<double> second)
    {
        double meanFirst = 0,
            meanSecond = 0;
        for (var i = 0; i < first.Length; i++)
        {
            meanFirst += first[i] / first.Length;
            meanSecond += second[i] / first.Length;
        }
        double product = 0,
            energyFirst = 0,
            energySecond = 0;
        for (var i = 0; i < first.Length; i++)
        {
            double a = first[i] - meanFirst,
                b = second[i] - meanSecond;
            product += a * b;
            energyFirst += a * a;
            energySecond += b * b;
        }
        return energyFirst * energySecond > 0 ? product / Math.Sqrt(energyFirst * energySecond) : 0;
    }
}
