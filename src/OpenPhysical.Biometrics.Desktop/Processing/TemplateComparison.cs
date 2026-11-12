// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Cache-aware comparison of exact canonical capture and reference templates.</summary>
/// <param name="comparison">The actual fingerprint comparison service.</param>
public sealed class TemplateComparison(FingerprintComparisonService comparison)
{
    private readonly object cacheGate = new();
    private Maybe<(
        CapturedFingerprint Capture,
        FingerPosition Finger,
        FingerprintTemplate Template
    )> probeCache;
    private Maybe<(
        FingerprintRecord Record,
        int View,
        FingerprintTemplate Template
    )> referenceCache;

    /// <summary>Invalidates both canonical template caches.</summary>
    /// <returns>Successful cache invalidation.</returns>
    public UnitResult<DesktopError> Clear()
    {
        lock (cacheGate)
        {
            referenceCache = Maybe<(FingerprintRecord, int, FingerprintTemplate)>.None;
            probeCache = Maybe<(CapturedFingerprint, FingerPosition, FingerprintTemplate)>.None;
        }
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Compares exact exported subsets with a verification-only probe assignment.</summary>
    /// <param name="probe">Immutable captured fingerprint.</param>
    /// <param name="assignedFinger">Explicit comparison assignment, leaving capture identity unchanged.</param>
    /// <param name="reference">Loaded canonical reference record.</param>
    /// <param name="viewIndex">Selected actual reference view index.</param>
    /// <param name="minimumScore">Inclusive nonnegative matching threshold.</param>
    /// <returns>The actual match result or a typed selection, conversion or engine failure.</returns>
    public Result<FingerprintComparisonResult, DesktopError> Compare(
        CapturedFingerprint probe,
        FingerPosition assignedFinger,
        FingerprintRecord reference,
        int viewIndex,
        int minimumScore
    )
    {
        if (viewIndex < 0 || viewIndex >= reference.Views.Length)
            return new DesktopError("ViewSelection", "Select an existing reference view.");
        var view = reference.Views[viewIndex];
        var finger = (FingerPosition)view.FingerPosition;
        return FingerprintMatchingPolicy
            .Create("desktop-bozorth3-v1", minimumScore)
            .MapError(Error)
            .Bind(policy =>
                Probe(probe, assignedFinger)
                    .Bind(probeTemplate =>
                        Reference(reference, viewIndex)
                            .Bind(referenceTemplate =>
                                comparison
                                    .Compare(
                                        probeTemplate,
                                        assignedFinger,
                                        referenceTemplate,
                                        finger,
                                        policy
                                    )
                                    .MapError(Error)
                            )
                    )
            );
    }

    private Result<FingerprintTemplate, DesktopError> Probe(
        CapturedFingerprint capture,
        FingerPosition assignedFinger
    )
    {
        lock (cacheGate)
            return CacheProbe(capture, assignedFinger);
    }

    private Result<FingerprintTemplate, DesktopError> CacheProbe(
        CapturedFingerprint capture,
        FingerPosition assignedFinger
    )
    {
        if (
            probeCache.HasValue
            && ReferenceEquals(probeCache.Value.Capture, capture)
            && probeCache.Value.Finger == assignedFinger
        )
            return probeCache.Value.Template;
        var converted = capture
            .CanonicalRecord(assignedFinger)
            .ToMatchingTemplate(assignedFinger, capture.View.ViewNumber)
            .MapError(Error);
        if (converted.IsSuccess)
            probeCache = (capture, assignedFinger, converted.Value);
        return converted;
    }

    private Result<FingerprintTemplate, DesktopError> Reference(FingerprintRecord record, int index)
    {
        lock (cacheGate)
            return CacheReference(record, index);
    }

    private Result<FingerprintTemplate, DesktopError> CacheReference(
        FingerprintRecord record,
        int index
    )
    {
        if (
            referenceCache.HasValue
            && ReferenceEquals(referenceCache.Value.Record, record)
            && referenceCache.Value.View == index
        )
            return referenceCache.Value.Template;
        var view = record.Views[index];
        var converted = record
            .ToMatchingTemplate((FingerPosition)view.FingerPosition, view.ViewNumber)
            .MapError(Error);
        if (converted.IsSuccess)
            referenceCache = (record, index, converted.Value);
        return converted;
    }

    private static DesktopError Error(FingerprintError error) => new(error.Code, error.Message);
}
