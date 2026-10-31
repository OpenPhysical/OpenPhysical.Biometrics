// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;

// A capture station supplies finger identity and equipment evidence with each
// image. Keep those facts attached while assessing several acquisition attempts.
internal static class EnrollmentExample
{
    internal static CbeffRecord Prepare(
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
        var candidates = captures.Select(enrollment.Prepare).ToImmutableArray();
        var ranked = PivFingerprintEnrollmentService.RankCandidates(candidates, preferLeftHand);
        var selections = ranked.ToDictionary(candidate => candidate.Finger);

        // Selection remains explicit after the operator reviews candidate quality
        // and any reacquisition recommendation. Each selected finger has one view.
        var record = enrollment.CreateRecord(
            selections[primaryFinger],
            selections[secondaryFinger],
            equipment
        );
        var findings = PivBiometricProfile.Validate(record);
        if (!findings.IsEmpty)
            throw new InvalidOperationException("The selected enrollment needs profile review.");
        var encoded = Incits378Codec.Write(record);

        // The PIV patron header identifies a processed fingerprint record. The
        // signing workflow resolves the final detached-block length afterwards.
        var acquisitionTime = new[]
        {
            selections[primaryFinger].Capture,
            selections[secondaryFinger].Capture,
        }
            .OfType<FingerprintCapture>()
            .Select(capture => capture.CapturedAt)
            .DefaultIfEmpty(createdAt)
            .Min();
        var headerQuality =
            record.Views.Any(view => view.Quality <= 100)
                ? checked(
                    (sbyte)record.Views.Where(view => view.Quality <= 100).Max(view => view.Quality)
                )
            : record.Views.Any(view => view.Quality == 255) ? (sbyte)-1
            : (sbyte)-2;
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
            headerQuality,
            creator,
            identity.FascN.AsSpan(),
            securityOptions: 0x0D
        );
        return new CbeffRecord(header, ImmutableArray.Create(encoded), []);
    }

    internal static FingerprintEnrollmentFinger RecordUnavailable(
        FingerPosition finger,
        FingerprintAvailability availability
    )
    {
        // The acquisition station records why a finger is unusable. The record
        // writer preserves its temporary or permanent status in the selected view.
        return FingerprintEnrollmentFinger.Unavailable(finger, availability);
    }
}
