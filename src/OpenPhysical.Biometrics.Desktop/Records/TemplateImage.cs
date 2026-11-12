// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>A verified grayscale view and its optional Studio processing evidence.</summary>
/// <param name="ViewIndex">Zero-based record view index.</param>
/// <param name="Finger">The declared finger position.</param>
/// <param name="Width">Image width in source pixels.</param>
/// <param name="Height">Image height in source pixels.</param>
/// <param name="GrayPixels">Exact row-major grayscale pixels.</param>
/// <param name="CapturedAtUtc">The recorded UTC acquisition instant.</param>
public sealed record TemplateImage(
    int ViewIndex,
    FingerPosition Finger,
    int Width,
    int Height,
    ImmutableArray<byte> GrayPixels,
    DateTimeOffset CapturedAtUtc
)
{
    /// <summary>Gets the supplemental quality assessment when available.</summary>
    public Maybe<FingerprintUnifiedQualityAssessment> Nfiq2 { get; init; }

    /// <summary>Gets the verified Studio evidence when a package was supplied.</summary>
    public Maybe<TemplateProcessingDetails> Processing { get; init; }

    /// <summary>Gets the extractor identity supplied by the capture service.</summary>
    public Maybe<string> ExtractorVersion { get; init; }
}
