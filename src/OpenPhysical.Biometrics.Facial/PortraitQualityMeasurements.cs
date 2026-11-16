// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Source-bound advisory evidence; no combined score or automatic acceptance threshold.</summary>
/// <param name="SourceSha256">Identity of the reviewed source.</param>
/// <param name="PortraitSha256">Digest of the exact prepared RGB pixels measured.</param>
/// <param name="MaskedLaplacianVariance">Raw normalized LoG variance, absent when not assessable.</param>
/// <param name="Backdrop">Conservative background evidence.</param>
/// <param name="Findings">Remaining measurement and human review requirements.</param>
public sealed record PortraitQualityMeasurements(
    string SourceSha256,
    string PortraitSha256,
    Maybe<double> MaskedLaplacianVariance,
    PortraitBackdropMeasurements Backdrop,
    ImmutableArray<PortraitFinding> Findings
)
{
    /// <summary>Gets the versioned measurement policy.</summary>
    public string PolicyId => "portrait-advisory-v1";

    /// <summary>Gets the mask and statistic coordinate system.</summary>
    public string Coordinates => "prepared-portrait-top-left";
}
