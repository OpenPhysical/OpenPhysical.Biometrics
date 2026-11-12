// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Application choices for RAM-only empty-sensor learning.</summary>
public sealed record Fs50EmptySensorOptions
{
    /// <summary>Gets minimum distinct stable empty frames.</summary>
    public int RequiredFrames { get; init; } = 4;

    /// <summary>Gets minimum monotonic observation span.</summary>
    public int MinimumSpanMilliseconds { get; init; } = 750;

    /// <summary>Gets the bounded number of exposure references retained per session.</summary>
    public int MaximumReferences { get; init; } = 2;

    /// <summary>Gets the maximum gap allowed within a stable empty run.</summary>
    public int MaximumFrameGapMilliseconds { get; init; } = 1000;

    /// <summary>Gets maximum mean absolute change during empty learning.</summary>
    public double MaximumStableMeanAbsoluteDifference { get; init; } = 2;

    /// <summary>Gets minimum matched-reference contact-region difference before trusting a presentation.</summary>
    public double MinimumPresentationMeanAbsoluteDifference { get; init; } = 2;

    /// <summary>Gets minimum mean gray for confidently empty, illuminated glass.</summary>
    public double MinimumEmptyMeanGray { get; init; } = 230;
}
