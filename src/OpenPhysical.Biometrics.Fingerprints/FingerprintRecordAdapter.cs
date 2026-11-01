// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using OpenPhysical.Biometrics.Formats.Fingerprints;
using MatchingTemplate = OpenPhysical.Biometrics.NBIS.FingerprintTemplate;

/// <summary>Converts an explicitly selected canonical fingerprint view into an NBIS matching template.</summary>
public static class FingerprintRecordAdapter
{
    /// <summary>Converts one finger and view while preserving the source image scale.</summary>
    /// <param name="record">An already parsed or authenticated fingerprint record.</param>
    /// <param name="finger">The finger to select.</param>
    /// <param name="viewNumber">The selected view identifier, from zero through fifteen.</param>
    /// <returns>An immutable nominal 500 ppi NBIS template with height-minus-row coordinates.</returns>
    /// <remarks>Pixel scale and minutia type are retained. INCITS two-degree angles become whole degrees
    /// with a half-turn direction reversal. Every selected point is copied into the matching template.
    /// Views exceeding 150 points require reported quality for Bozorth3's point ranking.</remarks>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Finger or view is invalid.</exception>
    /// <exception cref="ArgumentException">The selected view is absent, duplicated, or invalid.</exception>
    /// <exception cref="InvalidOperationException">The selected finger is marked unusable.</exception>
    /// <exception cref="NotSupportedException">Resolution or minutia-quality representation is unsupported.</exception>
    public static MatchingTemplate ToMatchingTemplate(
        this FingerprintRecord record,
        FingerPosition finger,
        byte viewNumber
    )
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!Enum.IsDefined(finger))
            throw new ArgumentOutOfRangeException(nameof(finger));
        if (viewNumber > 15)
            throw new ArgumentOutOfRangeException(nameof(viewNumber));
        if (
            record.XResolution != FingerprintGeometry.NominalPixelsPerCentimeter
            || record.YResolution != FingerprintGeometry.NominalPixelsPerCentimeter
        )
            throw new NotSupportedException(
                "Matching requires isotropic 197 pixels/cm, the nominal 500 ppi encoding."
            );
        if (record.Views.IsDefault || record.Views.Any(view => view is null))
            throw new ArgumentException(
                "The record requires initialized fingerprint views.",
                nameof(record)
            );
        var selected = record
            .Views.Where(view =>
                view.FingerPosition == (byte)finger && view.ViewNumber == viewNumber
            )
            .Take(2)
            .ToArray();
        if (selected.Length != 1)
            throw new ArgumentException(
                "Select exactly one existing finger and view.",
                nameof(record)
            );
        var view = selected[0];
        if (view.Quality is 254 or 255)
            throw new InvalidOperationException("The selected finger is unavailable.");
        if (view.Quality > 100 || view.Minutiae.IsDefault || view.Minutiae.Length > byte.MaxValue)
            throw new ArgumentException(
                "The selected view has invalid quality or minutia count.",
                nameof(record)
            );
        // Bozorth3 ranks at most 150 points. When quality is unreported,
        // the caller selects the subset explicitly.
        if (view.Minutiae.Length > 150 && view.Minutiae.Any(point => point is { Quality: 0 }))
            throw new NotSupportedException(
                "Select at most 150 minutiae when their quality is unreported."
            );
        return new MatchingTemplate(
            record.Width,
            record.Height,
            view.Minutiae.Select(point =>
                FingerprintGeometry.ToMatching(point, record.Width, record.Height)
            ),
            "OpenPhysical INCITS 378-2004 to NBIS XYT; nominal 500 ppi"
        );
    }
}
