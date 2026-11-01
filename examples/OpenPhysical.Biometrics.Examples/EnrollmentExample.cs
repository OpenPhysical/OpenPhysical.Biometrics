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
        // Prepare performs extraction and quality assessment. Materialize it once
        // so grouping, review, and issuance reuse the same expensive results.
        var prepared = captures.Select(enrollment.Prepare).ToImmutableArray();
        if (prepared.Any(candidate => candidate.IsFailure))
            return Result.Failure<CbeffRecord>("A capture could not be prepared.");
        var ranked = PivFingerprintEnrollmentService.RankCandidates(
            prepared.Select(candidate => candidate.Value),
            preferLeftHand
        );
        if (ranked.IsFailure)
            return Result.Failure<CbeffRecord>(ranked.Error.Message);
        var selections = ranked.Value.ToDictionary(candidate => candidate.Finger);

        // Selection remains explicit after the operator reviews candidate quality
        // and any reacquisition recommendation. Each selected finger has one view.
        var record = enrollment.CreateRecord(
            selections[primaryFinger],
            selections[secondaryFinger],
            equipment
        );
        if (record.IsFailure)
            return Result.Failure<CbeffRecord>(record.Error.Message);
        if (!PivBiometricProfile.Validate(record.Value).IsEmpty)
            return Result.Failure<CbeffRecord>("The selected enrollment needs profile review.");
        var encoded = Incits378Codec.Write(record.Value);
        if (encoded.IsFailure)
            return Result.Failure<CbeffRecord>(encoded.Error.Message);

        // The PIV patron header identifies a processed fingerprint record. The
        // signing workflow resolves the final detached-block length afterwards.
        var acquisitionTime = new[]
        {
            selections[primaryFinger].Capture,
            selections[secondaryFinger].Capture,
        }
            .Where(capture => capture.HasValue)
            .Select(capture => capture.Value.CapturedAt)
            .DefaultIfEmpty(createdAt)
            .Min();
        var header = CbeffHeader.Create(
            (uint)encoded.Value.Length,
            0,
            0x001B,
            0x0201,
            acquisitionTime,
            createdAt,
            expiresAt,
            8,
            0x80,
            HeaderQuality(record.Value),
            creator,
            identity.FascN.AsSpan(),
            securityOptions: 0x0D
        );
        return header.IsFailure
            ? Result.Failure<CbeffRecord>(header.Error.Message)
            : new CbeffRecord(header.Value, ImmutableArray.Create(encoded.Value), []);
    }

    internal static Result<FingerprintEnrollmentFinger, FingerprintError> RecordUnavailable(
        FingerPosition finger,
        FingerprintAvailability availability
    ) =>
        // The acquisition station records why a finger is unusable. The record
        // writer preserves its temporary or permanent status in the selected view.
        FingerprintEnrollmentFinger.Unavailable(finger, availability);

    private static sbyte HeaderQuality(FingerprintRecord record) =>
        record.Views.Any(view => view.Quality <= 100)
            ? (sbyte)record.Views.Where(view => view.Quality <= 100).Max(view => view.Quality)
        : record.Views.Any(view => view.Quality == 255) ? (sbyte)-1
        : (sbyte)-2;
}
