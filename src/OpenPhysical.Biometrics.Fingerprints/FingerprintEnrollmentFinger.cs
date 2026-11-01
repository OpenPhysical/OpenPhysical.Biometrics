// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;

/// <summary>A selected enrollment finger, including usable captures and unavailable states.</summary>
public sealed class FingerprintEnrollmentFinger
{
    private FingerprintEnrollmentFinger(
        FingerPosition finger,
        FingerprintAvailability availability,
        Maybe<FingerprintCapture> capture,
        Maybe<FingerprintTemplate> template,
        Maybe<FingerprintQualityAssessment> quality
    )
    {
        Finger = finger;
        Availability = availability;
        Capture = capture;
        Template = template;
        Quality = quality;
    }

    /// <summary>Gets the selected finger.</summary>
    public FingerPosition Finger { get; }

    /// <summary>Gets the availability recorded for issuance.</summary>
    public FingerprintAvailability Availability { get; }

    /// <summary>Gets the capture when this finger is available.</summary>
    public Maybe<FingerprintCapture> Capture { get; }

    /// <summary>Gets the extracted matching template when this finger is available.</summary>
    public Maybe<FingerprintTemplate> Template { get; }

    /// <summary>Gets the parent image's NFIQ 1 assessment when available.</summary>
    public Maybe<FingerprintQualityAssessment> Quality { get; }

    /// <summary>Gets whether quality or a low minutia count recommends another acquisition.</summary>
    public bool ReacquisitionRecommended =>
        Availability == FingerprintAvailability.Available
        && Quality
            .Bind(quality =>
                Template.Map(template =>
                    quality.Status != FingerprintQualityStatus.Assessed
                    || quality.Nfiq1 > 3
                    || template.Minutiae.Length <= 10
                )
            )
            .GetValueOrDefault(false);

    /// <summary>Records a finger that is temporarily or permanently unusable.</summary>
    /// <param name="finger">The unavailable finger.</param>
    /// <param name="availability">The temporary or permanent unavailable state.</param>
    /// <returns>An enrollment selection containing no captured biometric data, or an error when a finger or unavailable state is invalid.</returns>
    public static Result<FingerprintEnrollmentFinger, FingerprintError> Unavailable(
        FingerPosition finger,
        FingerprintAvailability availability
    ) =>
        FingerprintChecks
            .Require(
                Enum.IsDefined(finger),
                FingerprintErrorCodes.InvalidFinger,
                "The finger position is not an ANSI finger code."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    availability
                        is FingerprintAvailability.TemporarilyUnusable
                            or FingerprintAvailability.PermanentlyUnusable,
                    FingerprintErrorCodes.InvalidAvailability,
                    "An unavailable finger is temporarily or permanently unusable."
                )
            )
            .Map(() =>
                new FingerprintEnrollmentFinger(
                    finger,
                    availability,
                    Maybe<FingerprintCapture>.None,
                    Maybe<FingerprintTemplate>.None,
                    Maybe<FingerprintQualityAssessment>.None
                )
            );

    internal static FingerprintEnrollmentFinger Available(
        FingerprintCapture capture,
        FingerprintTemplate template,
        FingerprintQualityAssessment quality
    ) => new(capture.Finger, FingerprintAvailability.Available, capture, template, quality);
}
