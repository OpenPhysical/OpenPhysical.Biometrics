// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal sealed record InspectionComparisonRequest(
    CapturedFingerprint Probe,
    FingerPosition AssignedFinger,
    FingerprintRecord Reference,
    int View,
    int Threshold,
    int Generation,
    int DecisionRevision
)
{
    internal static Result<InspectionComparisonRequest, DesktopError> Create(
        Maybe<TemplateDocument> document,
        Maybe<CapturedFingerprint> probe,
        FingerPosition assignedFinger,
        int view,
        int threshold,
        int generation,
        int decisionRevision
    ) =>
        document
            .ToResult(new DesktopError("ReferenceMissing", "Open a reference template first."))
            .Bind(reference =>
                probe
                    .ToResult(new DesktopError("CaptureMissing", "Capture a finger first."))
                    .Map(capture => new InspectionComparisonRequest(
                        capture,
                        assignedFinger,
                        reference.Record,
                        view,
                        threshold,
                        generation,
                        decisionRevision
                    ))
            );

    internal bool IsCurrent(
        Maybe<TemplateDocument> document,
        Maybe<CapturedFingerprint> probe,
        FingerPosition assignedFinger,
        int view,
        int threshold,
        int generation,
        int decisionRevision
    ) =>
        Generation == generation
        && DecisionRevision == decisionRevision
        && AssignedFinger == assignedFinger
        && View == view
        && Threshold == threshold
        && document.HasValue
        && ReferenceEquals(document.Value.Record, Reference)
        && probe.HasValue
        && ReferenceEquals(probe.Value, Probe);
}
