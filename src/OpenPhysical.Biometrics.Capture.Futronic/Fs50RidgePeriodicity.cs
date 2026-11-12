// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50RidgePeriodicity
{
    internal static byte Measure(ReadOnlySpan<double> profile, Fs50EvidenceOptions options)
    {
        byte best = 0;
        double strongest = options.MinimumPeriodCorrelation;
        for (var period = options.MinimumPeriod; period <= options.MaximumPeriod; period++)
        {
            var correlation = Correlation(profile, period);
            if (
                correlation < strongest
                || Correlation(profile, period / 2) > options.MaximumHalfPeriodCorrelation
            )
                continue;
            strongest = correlation;
            best = (byte)period;
        }
        return best;
    }

    private static double Correlation(ReadOnlySpan<double> profile, int lag) =>
        Fs50RidgeCorrelation.Coefficient(profile[..^lag], profile[lag..]);
}
