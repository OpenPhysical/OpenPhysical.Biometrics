// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;

// A capture station supplies finger identity and equipment evidence with each
// image. Keep those facts attached while assessing several acquisition attempts.
internal static class EnrollmentExample
{
    internal static Result<CbeffRecord> Prepare(
        IEnumerable<FingerprintCapture> captures,
        PivFingerprintEnrollmentService enrollment,
        PivFingerprintRecordMetadata equipment,
        FingerPosition primaryFinger,
        FingerPosition secondaryFinger,
        bool preferLeftHand,
        BiometricIdentity identity,
        ReadOnlySpan<byte> creator,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt
    )
    {
        var selections = Rank(captures, enrollment, preferLeftHand);
        if (selections.IsFailure)
            return Result.Failure<CbeffRecord>(selections.Error);

        // Selection remains explicit after the operator reviews candidate quality
        // and any reacquisition recommendation. Each selected finger has one view.
        var primary = selections.Value[primaryFinger];
        var secondary = selections.Value[secondaryFinger];
        var record = CreateValidatedRecord(enrollment, primary, secondary, equipment);
        if (record.IsFailure)
            return Result.Failure<CbeffRecord>(record.Error);
        var encoded = Incits378Codec.Write(record.Value);
        if (encoded.IsFailure)
            return Result.Failure<CbeffRecord>(encoded.Error.Message);
        var acquired = AcquisitionTime(primary, secondary, createdAt);
        return Wrap(record.Value, encoded.Value, identity, creator, acquired, createdAt, expiresAt);
    }

    internal static Result<FingerprintEnrollmentFinger, FingerprintError> RecordUnavailable(
        FingerPosition finger,
        FingerprintAvailability availability
    ) =>
        // The acquisition station records why a finger is unusable. The record
        // writer preserves its temporary or permanent status in the selected view.
        FingerprintEnrollmentFinger.Unavailable(finger, availability);

    // Prepare performs extraction and quality assessment. Materialize it once
    // so grouping, review, and issuance reuse the same expensive results.
    private static Result<Dictionary<FingerPosition, FingerprintEnrollmentFinger>> Rank(
        IEnumerable<FingerprintCapture> captures,
        PivFingerprintEnrollmentService enrollment,
        bool preferLeftHand
    )
    {
        var prepared = captures.Select(enrollment.Prepare).ToImmutableArray();
        if (prepared.Any(candidate => candidate.IsFailure))
            return Result.Failure<Dictionary<FingerPosition, FingerprintEnrollmentFinger>>(
                "A capture could not be prepared."
            );
        var ranked = PivFingerprintEnrollmentService.RankCandidates(
            prepared.Select(candidate => candidate.Value),
            preferLeftHand
        );
        return ranked.IsFailure
            ? Result.Failure<Dictionary<FingerPosition, FingerprintEnrollmentFinger>>(
                ranked.Error.Message
            )
            : ranked.Value.ToDictionary(candidate => candidate.Finger);
    }

    private static Result<FingerprintRecord> CreateValidatedRecord(
        PivFingerprintEnrollmentService enrollment,
        FingerprintEnrollmentFinger primary,
        FingerprintEnrollmentFinger secondary,
        PivFingerprintRecordMetadata equipment
    )
    {
        var record = enrollment.CreateRecord(primary, secondary, equipment);
        if (record.IsFailure)
            return Result.Failure<FingerprintRecord>(record.Error.Message);
        return PivBiometricProfile.Validate(record.Value).IsEmpty
            ? record.Value
            : Result.Failure<FingerprintRecord>("The selected enrollment needs profile review.");
    }

    private static DateTimeOffset AcquisitionTime(
        FingerprintEnrollmentFinger primary,
        FingerprintEnrollmentFinger secondary,
        DateTimeOffset fallback
    ) =>
        new[] { primary.Capture, secondary.Capture }
            .Choose()
            .Select(capture => capture.CapturedAt)
            .DefaultIfEmpty(fallback)
            .Min();

    // The PIV patron header identifies a processed fingerprint record. The
    // signing workflow resolves the final detached-block length afterwards.
    private static Result<CbeffRecord> Wrap(
        FingerprintRecord record,
        byte[] encoded,
        BiometricIdentity identity,
        ReadOnlySpan<byte> creator,
        DateTimeOffset acquisitionTime,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt
    )
    {
        var header = CbeffHeader.Create(
            (uint)encoded.Length,
            0,
            0x001B,
            0x0201,
            acquisitionTime,
            createdAt,
            expiresAt,
            8,
            0x80,
            HeaderQuality(record),
            creator,
            identity.FascN.AsSpan(),
            securityOptions: 0x0D
        );
        return header.IsFailure
            ? Result.Failure<CbeffRecord>(header.Error.Message)
            : new CbeffRecord(header.Value, ImmutableArray.Create(encoded), []);
    }

    private static sbyte HeaderQuality(FingerprintRecord record) =>
        record.Views.Any(view => view.Quality <= 100)
            ? (sbyte)record.Views.Where(view => view.Quality <= 100).Max(view => view.Quality)
        : record.Views.Any(view => view.Quality == 255) ? (sbyte)-1
        : (sbyte)-2;
}
