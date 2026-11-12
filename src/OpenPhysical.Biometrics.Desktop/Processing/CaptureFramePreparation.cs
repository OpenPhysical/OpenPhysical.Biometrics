// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal static class CaptureFramePreparation
{
    internal static Result<CapturePreparedFrame, DesktopError> Prepare(
        CaptureFrame original,
        CaptureProcessingRequest request,
        CancellationToken token
    ) =>
        CaptureProcessingCancellation
            .Check(token)
            .Bind(() => FingerprintForeground.Analyze(original, request.Evidence))
            .Bind(region =>
                Fs50ContactContourRegion
                    .Create(original, region.Diagnostics, request.Contour, token)
                    .MapError(error => new DesktopError(error.Code, error.Message))
                    .Bind(contour => Process(original, request, region.Diagnostics, contour, token))
            );

    internal static Result<CapturePreparedFrame, DesktopError> Process(
        CaptureFrame original,
        CaptureProcessingRequest request,
        Fs50ContactDiagnostics diagnostics,
        Fs50ContactContourArtifact contour,
        CancellationToken token
    ) =>
        request.Input is null
            ? new DesktopError("Capture.ProcessingInput", "Explicit input provenance is required.")
            : contour
                .WithContext(request.Contour, token)
                .MapError(error => new DesktopError(error.Code, error.Message))
                .Bind(current =>
                    request
                        .Input.Resolve(original, current)
                        .Bind(
                            (Maybe<Fs50WholeContactProof> proof) =>
                                Pixels(original, request, diagnostics, current, proof, token)
                        )
                )
                .Check(CapturePreparedValidation.Check);

    private static Result<CapturePreparedFrame, DesktopError> Pixels(
        CaptureFrame original,
        CaptureProcessingRequest request,
        Fs50ContactDiagnostics diagnostics,
        Fs50ContactContourArtifact contour,
        Maybe<Fs50WholeContactProof> proof,
        CancellationToken token
    ) =>
        contour
            .ToProcessingRegion(!request.Preprocessing.IsOriginal)
            .MapError(error => new DesktopError(error.Code, error.Message))
            .Bind(region =>
                FingerprintPreprocessing
                    .Process(original, region.Mask, request.Preprocessing, request.Revision, token)
                    .MapError(error => new DesktopError(error.Code, error.Message))
                    .Map(artifact => new CapturePreparedFrame(
                        artifact,
                        diagnostics,
                        contour,
                        region,
                        request.Input.Role,
                        proof
                    ))
            );
}
