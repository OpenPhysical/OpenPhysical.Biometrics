// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

internal static class EvaluationCapture
{
    internal static Result<CapturedFingerprint, DesktopError> Create(
        CaptureFrame frame,
        PreparedFingerprint prepared,
        ImmutableArray<byte> mask,
        Maybe<FingerprintUnifiedQualityAssessment> nfiq2
    ) =>
        FingerprintRegionSelection
            .Select(prepared.Template, mask)
            .MapError(error => new DesktopError(error.Code, error.Message))
            .Ensure(
                selected => selected.Minutiae.Length > 0,
                new DesktopError(
                    "Capture.NoFeatures",
                    "No features remain in the declared image region."
                )
            )
            .Bind(selected =>
                FingerprintViewFactory
                    .Create(
                        selected,
                        prepared.Quality,
                        FingerPosition.RightIndex,
                        0,
                        FingerprintImpression.PlainLiveScan
                    )
                    .MapError(error => new DesktopError(error.Code, error.Message))
                    .Map(view => new CapturedFingerprint(
                        frame,
                        FingerPosition.RightIndex,
                        view,
                        prepared.Quality
                    )
                    {
                        Nfiq2 = nfiq2,
                        ExtractedCount = prepared.Template.Minutiae.Length,
                        ForegroundCount = selected.Minutiae.Length,
                        ExtractorVersion = prepared.Template.Provenance,
                    })
            );
}
