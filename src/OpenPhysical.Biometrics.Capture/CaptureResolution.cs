// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>Resolution evidence; unknown resolution is never substituted with 500 ppi.</summary>
public sealed record CaptureResolution
{
    private CaptureResolution(EvidenceKind kind, Maybe<double> x, Maybe<double> y, string reference)
    {
        Kind = kind;
        HorizontalPpi = x;
        VerticalPpi = y;
        EvidenceReference = reference;
    }

    /// <summary>The source of the physical scale.</summary>
    public enum EvidenceKind
    {
        /// <summary>No physical scale is known.</summary>
        Unknown,

        /// <summary>The manufacturer declares the scale.</summary>
        Declared,

        /// <summary>A recorded physical measurement establishes the scale.</summary>
        Measured,
    }

    /// <summary>Gets the evidence category.</summary>
    public EvidenceKind Kind { get; }

    /// <summary>Gets the horizontal pixels per inch when known.</summary>
    public Maybe<double> HorizontalPpi { get; }

    /// <summary>Gets the vertical pixels per inch when known.</summary>
    public Maybe<double> VerticalPpi { get; }

    /// <summary>Gets the measurement or declaration reference, empty only when unknown.</summary>
    public string EvidenceReference { get; }

    /// <summary>Gets explicit absence of physical-scale evidence.</summary>
    public static CaptureResolution Unknown { get; } =
        new(EvidenceKind.Unknown, Maybe<double>.None, Maybe<double>.None, string.Empty);

    /// <summary>Creates declared or measured resolution without upgrading its evidentiary status.</summary>
    /// <param name="kind">Declared or measured evidence.</param>
    /// <param name="horizontalPpi">Horizontal pixels per inch.</param>
    /// <param name="verticalPpi">Vertical pixels per inch.</param>
    /// <param name="reference">Bounded evidence reference.</param>
    /// <returns>Resolution evidence or a calibration failure.</returns>
    public static Result<CaptureResolution, CaptureError> Create(
        EvidenceKind kind,
        double horizontalPpi,
        double verticalPpi,
        string reference
    ) =>
        kind is EvidenceKind.Declared or EvidenceKind.Measured
        && double.IsFinite(horizontalPpi)
        && horizontalPpi > 0
        && horizontalPpi <= 10000
        && double.IsFinite(verticalPpi)
        && verticalPpi > 0
        && verticalPpi <= 10000
        && !string.IsNullOrWhiteSpace(reference)
        && reference.Length <= 256
            ? new CaptureResolution(kind, horizontalPpi, verticalPpi, reference)
            : new CaptureError(
                CaptureErrorCodes.CalibrationInvalid,
                "Resolution evidence is invalid."
            );
}
