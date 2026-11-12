// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal static class CaptureViewFactory
{
    internal static Result<CapturedFingerprint, DesktopError> Create(
        CaptureFrame original,
        FingerPosition finger,
        PreparedFingerprint prepared,
        ImmutableArray<byte> featureMask,
        Maybe<Fs50ContactRegion> segmentation,
        Maybe<FingerprintUnifiedQualityAssessment> nfiq2
    ) =>
        FingerprintForeground
            .Select(prepared.Template, featureMask)
            .Bind(selected =>
                FingerprintViewFactory
                    .Create(
                        selected,
                        prepared.Quality,
                        finger,
                        0,
                        FingerprintImpression.PlainLiveScan
                    )
                    .MapError(error => new DesktopError(error.Code, error.Message))
                    .Map(view => new CapturedFingerprint(original, finger, view, prepared.Quality)
                    {
                        Segmentation = segmentation,
                        ExtractedCount = prepared.Template.Minutiae.Length,
                        ForegroundCount = selected.Minutiae.Length,
                        Nfiq2 = nfiq2,
                        ExtractorVersion = prepared.Template.Provenance,
                    })
            );
}
