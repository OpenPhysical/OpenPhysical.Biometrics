// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record CaptureAdjustmentEvidence(
    FingerprintProcessingMask Mask,
    Maybe<Fs50ContactDiagnostics> Diagnostics
)
{
    internal Maybe<Fs50ContactRegion> Contact { get; init; }
    internal Maybe<Fs50ContactContourArtifact> Contour { get; init; }
    internal Maybe<Fs50ContourProcessingRegion> ProcessingRegion { get; init; }
    internal Maybe<ScoredFingerprintImage> Assessment { get; init; }
    internal Maybe<PreparedFingerprint> Extraction { get; init; }
    internal Maybe<ScoredFingerprintImage> OriginalAssessment { get; init; }
    internal Maybe<PreparedFingerprint> OriginalExtraction { get; init; }
    internal CaptureProcessingInput Input { get; init; } = CaptureProcessingInput.LiveSensor([]);

    internal static CaptureAdjustmentEvidence From(CaptureProcessingEvidence source) =>
        new(source.Artifact.Mask, source.ContactDiagnostics)
        {
            Assessment = source.RuntimeAssessment,
            Extraction = source.RuntimeExtraction,
            OriginalAssessment = source.RuntimeOriginalAssessment,
            OriginalExtraction = source.RuntimeOriginalExtraction,
            Contour = source.Contour,
            ProcessingRegion = source.ProcessingRegion,
            Input = CaptureProcessingInput.ForRevision(
                source.InputRole,
                source.RuntimeWholeContactProof
            ),
        };

    internal static CaptureAdjustmentEvidence From(CapturePreparedFrame source) =>
        new(source.Artifact.Mask, source.Diagnostics)
        {
            Assessment = source.CachedAssessment,
            Extraction = source.CachedExtraction,
            OriginalAssessment = source.CachedOriginalAssessment,
            OriginalExtraction = source.CachedOriginalExtraction,
            Contour = source.Contour,
            ProcessingRegion = source.ProcessingRegion,
            Input = CaptureProcessingInput.ForRevision(source.InputRole, source.WholeContactProof),
        };

    internal static Result<CaptureAdjustmentEvidence, DesktopError> ForChange(
        CaptureFrame frame,
        Maybe<CapturedFingerprint> source,
        Fs50EvidencePolicy policy,
        Maybe<CaptureProcessingInput> frozenInput
    ) =>
        source
            .Bind(value => value.Processing)
            .Map(From)
            .Match(
                value => Result.Success<CaptureAdjustmentEvidence, DesktopError>(value),
                () =>
                    frozenInput
                        .ToResult(MissingInput())
                        .Bind(input =>
                            Analyze(frame, policy).Map(value => value with { Input = input })
                        )
            );

    internal Result<CapturePreparedFrame, DesktopError> Prepare(
        CaptureFrame frame,
        CaptureProcessingRequest request,
        CancellationToken token
    ) =>
        (
            Contour.HasValue && Diagnostics.HasValue
                ? CaptureFramePreparation.Process(
                    frame,
                    request with
                    {
                        Evidence = Diagnostics.Value.Policy,
                    },
                    Diagnostics.Value,
                    Contour.Value,
                    token
                )
                : CaptureFramePreparation.Prepare(frame, request, token)
        ).Map(prepared =>
            prepared with
            {
                CachedAssessment = Assessment,
                CachedExtraction = Extraction,
                CachedOriginalAssessment = OriginalAssessment,
                CachedOriginalExtraction = OriginalExtraction,
            }
        );

    internal static Result<CaptureAdjustmentEvidence, DesktopError> Analyze(
        CaptureFrame frame,
        Fs50EvidencePolicy policy
    ) =>
        Fs50ContactAnalysis
            .Analyze(frame.Pixels.AsSpan(), policy)
            .Bind(region =>
                Whole(frame)
                    .Map(mask => new CaptureAdjustmentEvidence(mask, region.Diagnostics)
                    {
                        Contact = region,
                    })
            )
            .MapError(error => new DesktopError(error.Code, error.Message));

    private static Result<FingerprintProcessingMask, CaptureError> Whole(CaptureFrame frame)
    {
        var mask = Enumerable.Repeat((byte)1, frame.Width * frame.Height).ToArray();
        return FingerprintProcessingMask.Create(frame.Width, frame.Height, mask, mask);
    }

    internal static Result<CaptureAdjustmentEvidence, DesktopError> ForSource(
        Maybe<CapturedFingerprint> source,
        Maybe<DisplayedFingerprintFrame> displayed,
        Maybe<CaptureProcessingInput> frozenInput
    ) =>
        source
            .Bind(value => value.Processing)
            .Map(From)
            .Match(
                value => Result.Success<CaptureAdjustmentEvidence, DesktopError>(value),
                () =>
                    frozenInput
                        .ToResult(MissingInput())
                        .Bind(input =>
                            displayed
                                .Bind(value => value.Processing)
                                .Map(value => new CaptureAdjustmentEvidence(
                                    value.Mask,
                                    displayed
                                        .Bind(frame => frame.Segmentation)
                                        .Map(region => region.Diagnostics)
                                )
                                {
                                    Input = input,
                                })
                                .ToResult(
                                    new DesktopError(
                                        "Capture.Mask",
                                        "Capture a finger before adjusting its image."
                                    )
                                )
                        )
            );

    private static DesktopError MissingInput() =>
        new(
            "Capture.MissingFrozenInput",
            "The original acquisition evidence is unavailable. Reset this capture before adjusting it."
        );
}
