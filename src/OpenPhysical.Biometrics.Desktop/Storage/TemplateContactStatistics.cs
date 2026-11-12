// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateContactStatistics(int Area, double LowGray, double HighGray)
{
    internal static TemplateContactStatistics From(Fs50RegionStatistics value) =>
        new(value.Area, value.LowGray, value.HighGray);

    internal bool Valid(int pixels) =>
        Area >= 0
        && Area <= pixels
        && double.IsFinite(LowGray)
        && double.IsFinite(HighGray)
        && LowGray >= 0
        && HighGray >= LowGray
        && HighGray <= 255;
}
