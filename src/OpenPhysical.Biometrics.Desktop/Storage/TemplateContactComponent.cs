// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateContactComponent(
    TemplateContactStatistics Original,
    int SupportedArea,
    int RetainedArea,
    string Issue
)
{
    internal static TemplateContactComponent From(Fs50ComponentEvidence value) =>
        new(
            TemplateContactStatistics.From(value.Original),
            value.SupportedArea,
            value.RetainedArea,
            value.Issue.ToString()
        );

    internal bool Valid(int pixels) =>
        Original is not null
        && Original.Valid(pixels)
        && SupportedArea >= 0
        && RetainedArea >= 0
        && SupportedArea <= Original.Area
        && RetainedArea <= Original.Area
        && Enum.TryParse<Fs50ContactIssue>(Issue, out var issue)
        && Enum.IsDefined(issue);
}
