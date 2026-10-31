// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
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
    /// <exception cref="ArgumentNullException">A provider is null.</exception>
    public PivFingerprintEnrollmentService(
        IFingerprintEngine engine,
        IFingerprintQualityProvider qualityProvider
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(qualityProvider);
        this.engine = engine;
        this.qualityProvider = qualityProvider;
    }

    /// <summary>Assesses and extracts a capture once for subsequent enrollment selection.</summary>
    /// <param name="capture">The immutable original capture.</param>
    /// <returns>A selected finger with reusable template and quality assessment.</returns>
    /// <exception cref="ArgumentNullException">The capture is null.</exception>
    public FingerprintEnrollmentFinger Prepare(FingerprintCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var quality = qualityProvider.Assess(capture);
        return FingerprintEnrollmentFinger.Available(
            capture,
            engine.Extract(capture.Image),
            quality
        );
    }

    /// <summary>Selects the best captured attempt for each finger, then ranks fingers for operator review.</summary>
    /// <param name="candidates">Previously prepared available fingers; expensive extraction is already complete.</param>
    /// <param name="preferLeftHand">Whether the holder prefers the left hand.</param>
    /// <returns>One attempt per finger, ordered by quality and then preferred index, middle, thumb, ring, and little fingers.</returns>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    /// <exception cref="ArgumentException">A candidate is null or unavailable.</exception>
    public static ImmutableArray<FingerprintEnrollmentFinger> RankCandidates(
        IEnumerable<FingerprintEnrollmentFinger> candidates,
        bool preferLeftHand
    )
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var prepared = candidates.ToArray();
        if (
            prepared.Any(candidate =>
                candidate is null || candidate.Availability != FingerprintAvailability.Available
            )
        )
            throw new ArgumentException(
                "Candidates must contain available prepared fingers.",
                nameof(candidates)
            );
        return prepared
            .GroupBy(candidate => candidate.Finger)
            .Select(group =>
                group
                    .OrderBy(candidate => candidate.Quality!.Nfiq1)
                    .ThenByDescending(candidate => candidate.Template!.Minutiae.Length)
                    .ThenBy(candidate => candidate.Capture!.CapturedAt)
                    .First()
            )
            .OrderBy(candidate => candidate.Quality!.Nfiq1)
            .ThenBy(candidate => Preference(candidate.Finger, preferLeftHand))
            .ToImmutableArray();
    }

    /// <summary>Builds the primary and secondary views with the PIV minutiae limits and units.</summary>
    /// <param name="primary">The explicitly selected primary finger.</param>
    /// <param name="secondary">The explicitly selected secondary finger.</param>
    /// <param name="metadata">Registered identifiers and maximum original-image dimensions.</param>
    /// <returns>A canonical INCITS 378-2004 record ready for encoding and CBEFF preparation.</returns>
    /// <remarks>Coordinates retain the original pixel scale. Angles reverse the NBIS ridge direction
    /// and use two-degree INCITS units; odd-degree angles round down to the preceding encoded unit.</remarks>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">Fingers repeat, an unavailable primary precedes an available secondary, dimensions differ from source maxima, or equipment identifiers differ.</exception>
    public FingerprintRecord CreateRecord(
        FingerprintEnrollmentFinger primary,
        FingerprintEnrollmentFinger secondary,
        PivFingerprintRecordMetadata metadata
    )
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(secondary);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!metadata.EquipmentAppendixFCertified)
            throw new ArgumentException(
                "PIV enrollment requires Appendix F acquisition equipment.",
                nameof(metadata)
            );
        if (primary.Finger == secondary.Finger)
            throw new ArgumentException("Select two different fingers.", nameof(secondary));
        if (
            primary.Availability != FingerprintAvailability.Available
            && secondary.Availability == FingerprintAvailability.Available
        )
            throw new ArgumentException(
                "Select the available finger as the primary finger.",
                nameof(primary)
            );
        var captures = new[] { primary.Capture, secondary.Capture }
            .OfType<FingerprintCapture>()
            .ToArray();
        if (
            captures.Length > 0
            && (
                captures.Max(capture => capture.Image.Width) != metadata.Width
                || captures.Max(capture => capture.Image.Height) != metadata.Height
            )
        )
            throw new ArgumentException(
                "Record dimensions must equal the largest original dimensions.",
                nameof(metadata)
            );
        if (
            captures.Any(capture =>
                capture.EquipmentId != metadata.EquipmentId || !capture.EquipmentAppendixFCertified
            )
        )
            throw new ArgumentException(
                "Capture equipment identifiers must agree with the record metadata.",
                nameof(metadata)
            );
        return new FingerprintRecord(
            metadata.ProductOwner,
            metadata.ProductType,
            checked((ushort)(0x8000 | metadata.EquipmentId)),
            metadata.Width,
            metadata.Height,
            FingerprintGeometry.NominalPixelsPerCentimeter,
            FingerprintGeometry.NominalPixelsPerCentimeter,
            [CreateView(primary), CreateView(secondary)]
        );
    }

    private static FingerprintView CreateView(FingerprintEnrollmentFinger selection)
    {
        if (selection.Availability != FingerprintAvailability.Available)
            return new(
                (byte)selection.Finger,
                0,
                0,
                selection.Availability == FingerprintAvailability.PermanentlyUnusable
                    ? (byte)254
                    : (byte)255,
                [],
                []
            );
        var template = selection.Template!;
        // Selection is stable across runs. Coordinates are converted against each source image,
        // before the record header adopts the largest dimensions of the two source images.
        var points = template
            .Minutiae.OrderByDescending(point => point.Quality)
            .ThenBy(point => point.X)
            .ThenBy(point => point.Y)
            .ThenBy(point => point.Angle)
            .ThenBy(point => point.Type)
            .DistinctBy(point => (point.X, point.Y))
            .Take(128)
            .Select(point => FingerprintGeometry.ToRecord(point, template.Height))
            .ToImmutableArray();
        return new(
            (byte)selection.Finger,
            0,
            (byte)selection.Capture!.Impression,
            selection.Quality!.PivQuality,
            points,
            []
        );
    }

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
