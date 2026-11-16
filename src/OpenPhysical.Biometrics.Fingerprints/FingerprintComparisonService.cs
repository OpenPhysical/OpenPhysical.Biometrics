// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

/// <summary>Compares a fresh capture with an explicitly selected fingerprint reference.</summary>
public sealed class FingerprintComparisonService
{
    private readonly IFingerprintEngine engine;

    /// <summary>Creates a service using the selected fingerprint engine.</summary>
    /// <param name="engine">The extraction and matching implementation.</param>
    public FingerprintComparisonService(IFingerprintEngine engine)
    {
        this.engine = engine;
    }

    /// <summary>Compares a capture with a reference authenticated by the caller.</summary>
    /// <param name="probe">The fresh capture; its finger must match the selected reference.</param>
    /// <param name="reference">The caller-authenticated canonical reference record.</param>
    /// <param name="finger">The reference finger to select.</param>
    /// <param name="viewNumber">The explicit reference view number.</param>
    /// <param name="policy">The application's versioned matching policy.</param>
    /// <returns>The raw score and policy decision, or an error when the probe identifies a different finger, the selection is invalid, or the engine fails.</returns>
    public Result<FingerprintComparisonResult, FingerprintError> Compare(
        FingerprintCapture probe,
        FingerprintRecord reference,
        FingerPosition finger,
        byte viewNumber,
        FingerprintMatchingPolicy policy
    ) =>
        FingerprintChecks
            .Require(
                probe.Finger == finger,
                FingerprintErrorCodes.FingerMismatch,
                "The probe and selected reference fingers must agree."
            )
            .Bind(() => reference.ToMatchingTemplate(finger, viewNumber))
            .Bind(template =>
                FingerprintEdge
                    .Try(
                        () => engine.Extract(probe.Image),
                        FingerprintErrorCodes.EngineFailed,
                        "The fingerprint engine failed."
                    )
                    .Bind(extracted => Compare(extracted, probe.Finger, template, finger, policy))
            );

    /// <summary>Compares reusable templates without extracting either source image again.</summary>
    /// <param name="probe">Selected probe template, converted from the canonical exported view when available.</param>
    /// <param name="probeFinger">The finger selected for the probe template.</param>
    /// <param name="reference">Selected reference template in the same nominal 500 ppi NBIS coordinates.</param>
    /// <param name="referenceFinger">The finger selected for the reference template.</param>
    /// <param name="policy">The application's explicit versioned matching policy.</param>
    /// <returns>The raw score and policy decision, or an invalid finger, mismatch or engine failure.</returns>
    public Result<FingerprintComparisonResult, FingerprintError> Compare(
        FingerprintTemplate probe,
        FingerPosition probeFinger,
        FingerprintTemplate reference,
        FingerPosition referenceFinger,
        FingerprintMatchingPolicy policy
    ) =>
        FingerprintChecks
            .Finger(probeFinger)
            .Bind(() => FingerprintChecks.Finger(referenceFinger))
            .Bind(() =>
                FingerprintChecks.Require(
                    probeFinger == referenceFinger,
                    FingerprintErrorCodes.FingerMismatch,
                    "The probe and selected reference fingers must agree."
                )
            )
            .Bind(() =>
                FingerprintEdge.Try(
                    () => engine.Match(probe, reference),
                    FingerprintErrorCodes.EngineFailed,
                    "The fingerprint engine failed."
                )
            )
            .Bind(policy.Evaluate);
}
