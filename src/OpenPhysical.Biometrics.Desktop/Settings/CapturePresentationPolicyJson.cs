// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class CapturePresentationPolicyJson
{
    internal static object Write(FingerCapturePolicy value) =>
        new
        {
            candidateLimit = value.CandidateLimit,
            candidateWindowMilliseconds = value.CandidateWindow.TotalMilliseconds,
            acquisitionGapMilliseconds = value.AcquisitionGap.TotalMilliseconds,
            majorDisplacementFraction = value.MajorDisplacementFraction,
            minimumOverlap = value.MinimumOverlap,
            absenceFrames = value.AbsenceFrames,
            repositionFrames = value.RepositionFrames,
        };

    internal static Result<FingerCapturePolicy, DesktopError> Read(JsonElement value) =>
        DesktopEdge
            .Try(() => Parse(value), "Settings.Policy", "The saved presentation policy is invalid.")
            .Bind(result => result);

    private static Result<FingerCapturePolicy, DesktopError> Parse(JsonElement value) =>
        value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 7
            ? new DesktopError("Settings.Policy", "The saved presentation policy is invalid.")
            : FingerCapturePolicy
                .Create(
                    value.GetProperty("candidateLimit").GetInt32(),
                    TimeSpan.FromMilliseconds(
                        value.GetProperty("candidateWindowMilliseconds").GetDouble()
                    ),
                    TimeSpan.FromMilliseconds(
                        value.GetProperty("acquisitionGapMilliseconds").GetDouble()
                    ),
                    value.GetProperty("majorDisplacementFraction").GetDouble(),
                    value.GetProperty("minimumOverlap").GetDouble(),
                    value.GetProperty("absenceFrames").GetInt32(),
                    value.GetProperty("repositionFrames").GetInt32()
                )
                .MapError(error => new DesktopError(error.Code, error.Message));
}
