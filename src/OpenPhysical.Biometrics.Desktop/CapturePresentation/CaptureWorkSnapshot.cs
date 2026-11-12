// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class CaptureWorkSnapshot
{
    internal static CaptureWorkRequest Create(
        ImmutableArray<FingerCaptureCandidate> candidates,
        (FingerprintPreprocessingPolicy Policy, long Revision) image,
        CapturePolicySettings policy,
        TimeSpan delay,
        CaptureProcessingInput input
    ) =>
        new(
            candidates,
            new CaptureProcessingRequest(
                image.Policy,
                policy.Evidence,
                policy.Contour,
                image.Revision,
                input
            ),
            policy.Quality,
            new CaptureAcquisitionPolicy(policy, delay.TotalSeconds)
        );

    internal static Result<CaptureAcquisitionPolicy, DesktopError> OriginalPolicy(
        Maybe<CapturedFingerprint> source,
        Maybe<CaptureAcquisitionPolicy> frozen
    ) =>
        source
            .Bind(value => value.Processing)
            .Bind(value => value.OriginalAcquisitionPolicy)
            .Or(frozen)
            .ToResult(
                new DesktopError(
                    "Capture.MissingOriginalAcquisitionPolicy",
                    "Reset this capture before adjusting it again."
                )
            );
}
