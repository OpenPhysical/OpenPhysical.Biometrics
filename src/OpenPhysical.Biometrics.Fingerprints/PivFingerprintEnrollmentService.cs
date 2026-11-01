// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

/// <summary>Assesses captures and prepares the two selected fingers for PIV issuance.</summary>
public sealed class PivFingerprintEnrollmentService
{
    private readonly IFingerprintEngine engine;
    private readonly IFingerprintQualityProvider qualityProvider;

    /// <summary>Creates an enrollment service using explicit processing and NFIQ 1 providers.</summary>
    /// <param name="engine">The extraction and comparison engine.</param>
    /// <param name="qualityProvider">The parent-image NFIQ 1 provider.</param>
    public PivFingerprintEnrollmentService(
        IFingerprintEngine engine,
        IFingerprintQualityProvider qualityProvider
    )
    {
        this.engine = engine;
        this.qualityProvider = qualityProvider;
    }

    /// <summary>Assesses and extracts a capture once for subsequent enrollment selection.</summary>
    /// <param name="capture">The immutable original capture.</param>
    /// <returns>A selected finger with reusable template and quality assessment, or an error when assessment or extraction fails.</returns>
    public Result<FingerprintEnrollmentFinger, FingerprintError> Prepare(
        FingerprintCapture capture
    ) =>
        qualityProvider
            .Assess(capture)
            .Bind(quality =>
                FingerprintEdge
                    .Try(
                        () => engine.Extract(capture.Image),
                        FingerprintErrorCodes.EngineFailed,
                        "The fingerprint engine failed."
                    )
                    .Map(template =>
                        FingerprintEnrollmentFinger.Available(capture, template, quality)
                    )
            );

    /// <summary>Selects the best captured attempt for each finger, then ranks fingers for operator review.</summary>
    /// <param name="candidates">Previously prepared available fingers; expensive extraction is already complete.</param>
    /// <param name="preferLeftHand">Whether the holder prefers the left hand.</param>
    /// <returns>One attempt per finger, ordered by quality and then preferred index, middle, thumb, ring, and little fingers; or an error when a candidate is unavailable.</returns>
    public static Result<
        ImmutableArray<FingerprintEnrollmentFinger>,
        FingerprintError
    > RankCandidates(IEnumerable<FingerprintEnrollmentFinger> candidates, bool preferLeftHand)
    {
        var prepared = candidates.ToArray();
        return FingerprintChecks
            .Require(
                prepared.All(candidate =>
                    candidate.Availability == FingerprintAvailability.Available
                ),
                FingerprintErrorCodes.UnavailableCandidate,
                "Candidates must contain available prepared fingers."
            )
            .Map(() => Rank(prepared, preferLeftHand));
    }

    /// <summary>Builds the primary and secondary views with the PIV minutiae limits and units.</summary>
    /// <param name="primary">The explicitly selected primary finger.</param>
    /// <param name="secondary">The explicitly selected secondary finger.</param>
    /// <param name="metadata">Registered identifiers and maximum original-image dimensions.</param>
    /// <returns>A canonical INCITS 378-2004 record ready for encoding and CBEFF preparation, or an error when fingers repeat, an unavailable primary precedes an available secondary, dimensions differ from source maxima, or equipment identifiers differ.</returns>
    /// <remarks>Coordinates retain the original pixel scale. Angles reverse the NBIS ridge direction
    /// and use two-degree INCITS units; odd-degree angles round down to the preceding encoded unit.</remarks>
    public Result<FingerprintRecord, FingerprintError> CreateRecord(
        FingerprintEnrollmentFinger primary,
        FingerprintEnrollmentFinger secondary,
        PivFingerprintRecordMetadata metadata
    ) => PivEnrollmentRecordBuilder.Build(primary, secondary, metadata);

    private static ImmutableArray<FingerprintEnrollmentFinger> Rank(
        FingerprintEnrollmentFinger[] prepared,
        bool preferLeftHand
    ) =>
        prepared
            .GroupBy(candidate => candidate.Finger)
            .Select(group =>
                group
                    .OrderBy(candidate => candidate.Quality.Value.Nfiq1)
                    .ThenByDescending(candidate => candidate.Template.Value.Minutiae.Length)
                    .ThenBy(candidate => candidate.Capture.Value.CapturedAt)
                    .First()
            )
            .OrderBy(candidate => candidate.Quality.Value.Nfiq1)
            .ThenBy(candidate => Preference(candidate.Finger, preferLeftHand))
            .ToImmutableArray();

    private static int Preference(FingerPosition finger, bool left)
    {
        var code = (int)finger;
        var rank = ((code - 1) % 5) switch
        {
            1 => 0,
            2 => 1,
            0 => 2,
            3 => 3,
            _ => 4,
        };
        return rank * 2 + ((code > 5) == left ? 0 : 1);
    }
}
