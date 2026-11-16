// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal static class Fs50WholeContactRecordValidation
{
    internal static UnitResult<CaptureError> Source(
        Fs50WholeContactRecord record,
        FingerprintProcessingSource source,
        string driverVersion,
        CaptureGeometry geometry
    )
    {
        if (
            record.Identity is null
            || source is null
            || source.Sequence != record.Identity.Sequence
            || source.CapturedAt != record.Identity.CapturedAt
            || source.Exposure != record.Identity.Exposure
        )
            return Invalid();
        var key = Fs50EmptySensorKey.Create(
            source.DeviceId,
            record.Identity.SessionId,
            source.CalibrationDigest,
            source.CorrectionVersion,
            driverVersion,
            geometry,
            source.Exposure
        );
        return key.IsSuccess && key.Value.Digest == record.Identity.SessionKeyDigest
            ? UnitResult.Success<CaptureError>()
            : Invalid();
    }

    internal static UnitResult<CaptureError> Validate(
        Fs50WholeContactRecord record,
        Fs50ContactContourArtifact contour
    )
    {
        if (
            contour is null
            || record.Identity is null
            || record.Policy is null
            || record.Metrics is null
            || record.RidgeEvidence is null
            || record.ObservedMetrics is null
        )
            return Invalid();
        var identity = record.Identity;
        var valid =
            identity.SourceDigest == contour.SourceDigest
            && identity.ContactDigest == contour.ContactDigest
            && identity.FeatureDigest == contour.FeatureDigest
            && identity.EvidencePolicyDigest == contour.EvidencePolicyDigest
            && identity.EvidencePolicyDigest == record.Policy.Evidence.Digest
            && identity.ContourVersion == contour.Version
            && identity.Sequence >= 0
            && !string.IsNullOrWhiteSpace(identity.SessionId)
            && identity.SessionId.Length <= 256
            && !identity.SessionId.Any(char.IsControl)
            && Fs50PolicyDigest.IsDigest(identity.SessionKeyDigest)
            && record.ReferenceDigest.Match(Fs50PolicyDigest.IsDigest, () => true)
            && record.ReferenceKeyDigest.Match(Fs50PolicyDigest.IsDigest, () => true)
            && record.QualifiedAnchorDigest.Match(Fs50PolicyDigest.IsDigest, () => true)
            && Metrics(record.Metrics, contour.Statistics.SelectedContactArea)
            && Ridge(record.RidgeEvidence, contour.Statistics.SelectedContactArea)
            && Metrics(record.ObservedMetrics, record.RidgeEvidence.ObservedArea)
            && Decision(record);
        return valid ? UnitResult.Success<CaptureError>() : Invalid();
    }

    private static bool Metrics(Fs50WholeContactMetrics value, int contactArea) =>
        value.ContactArea == contactArea
        && value.CorroboratedArea >= 0
        && value.CorroboratedArea <= contactArea
        && value.UncorroboratedExteriorArea >= 0
        && value.UncorroboratedExteriorArea <= contactArea
        && (long)value.CorroboratedArea + value.UncorroboratedExteriorArea <= contactArea
        && value.StaticExteriorArea >= 0
        && value.UnobservedExteriorArea >= 0
        && (long)value.StaticExteriorArea + value.UnobservedExteriorArea
            == value.UncorroboratedExteriorArea
        && value.LargestUncorroboratedExteriorArea >= 0
        && value.LargestUncorroboratedExteriorArea <= value.UncorroboratedExteriorArea;

    private static bool Decision(Fs50WholeContactRecord record)
    {
        if (
            !Enum.IsDefined(record.Decision)
            || record.ReferenceDigest.HasValue != record.ReferenceKeyDigest.HasValue
        )
            return false;
        return record.Decision switch
        {
            Fs50WholeContactDecision.SpatialCapture or Fs50WholeContactDecision.SpatialExposure =>
                record.ReferenceDigest.HasNoValue
                    && record.ReferenceKeyDigest.HasNoValue
                    && record.QualifiedAnchorDigest.HasNoValue
                    && Unknown(record.Metrics)
                    && Unknown(record.ObservedMetrics),
            Fs50WholeContactDecision.QualifiedReference
            or Fs50WholeContactDecision.ExposureReference => record.ReferenceDigest.HasValue
                && record.ReferenceKeyDigest.Value == record.Identity.SessionKeyDigest
                && BelowThreshold(record),
            Fs50WholeContactDecision.QualifiedGeometry
            or Fs50WholeContactDecision.ExposureGeometry => record.QualifiedAnchorDigest.HasValue
                && record.ReferenceDigest.HasNoValue
                && BelowThreshold(record),
            Fs50WholeContactDecision.StaticExterior
            or Fs50WholeContactDecision.UnresolvedExterior => !BelowThreshold(record)
                && StaticConsistent(record),
            _ => true,
        };
    }

    private static bool Unknown(Fs50WholeContactMetrics value) =>
        value.CorroboratedArea == 0
        && value.UncorroboratedExteriorArea == value.ContactArea
        && value.StaticExteriorArea == 0
        && value.UnobservedExteriorArea == value.ContactArea
        && value.LargestUncorroboratedExteriorArea == value.ContactArea;

    private static bool StaticConsistent(Fs50WholeContactRecord record) =>
        record.Decision != Fs50WholeContactDecision.StaticExterior
        || Math.Max(record.Metrics.StaticExteriorArea, record.ObservedMetrics.StaticExteriorArea)
            >= record.Policy.Options.MinimumUncorroboratedArea;

    private static bool BelowThreshold(Fs50WholeContactRecord record) =>
        record.Metrics.LargestUncorroboratedExteriorArea
            < record.Policy.Options.MinimumUncorroboratedArea
        && record.ObservedMetrics.LargestUncorroboratedExteriorArea
            < record.Policy.Options.MinimumUncorroboratedArea;

    private static bool Ridge(Fs50WholeContactRidgeEvidence value, int contactArea) =>
        Fs50PolicyDigest.IsDigest(value.ObservedContactDigest)
        && value.ObservedArea >= contactArea
        && value.ObservedArea <= 800 * 750
        && value.RidgeExteriorArea >= 0
        && value.RidgeExteriorArea <= value.ObservedArea
        && value.LargestRidgeExteriorArea >= 0
        && value.LargestRidgeExteriorArea <= value.RidgeExteriorArea;

    private static CaptureError Invalid() =>
        new(
            CaptureErrorCodes.InvalidFrame,
            "Recorded whole-contact identity, decision or spatial counts are inconsistent."
        );
}
