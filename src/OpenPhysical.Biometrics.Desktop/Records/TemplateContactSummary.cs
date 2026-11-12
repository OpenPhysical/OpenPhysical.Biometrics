// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Measured counts from the saved contact assessment, separate from geometric ROI area.</summary>
/// <param name="CandidateArea">Measured candidate pixel count.</param>
/// <param name="RidgeSupportArea">Direct ridge support pixel count.</param>
/// <param name="AcceptedArea">Accepted contact-stage pixel count.</param>
/// <param name="RejectedArea">Rejected candidate pixel count.</param>
/// <param name="ComponentCount">Accepted component count.</param>
/// <param name="Issue">The recorded contact assessment outcome.</param>
public sealed record TemplateContactSummary(
    int CandidateArea,
    int RidgeSupportArea,
    int AcceptedArea,
    int RejectedArea,
    int ComponentCount,
    Fs50ContactIssue Issue
);
