// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Owned source pixels and their immutable, reusable face detections.</summary>
public sealed class PortraitAnalysis
{
    internal PortraitAnalysis(PortraitImage image, ImmutableArray<PortraitFace> faces)
    {
        Image = image;
        Faces = faces;
    }

    /// <summary>Gets the exact source image used for inference.</summary>
    public PortraitImage Image { get; }

    /// <summary>Gets detections in stable horizontal, then vertical order.</summary>
    public ImmutableArray<PortraitFace> Faces { get; }

    /// <summary>Gets frozen-source person confidence, absent during ordinary live detection.</summary>
    public Maybe<PortraitPersonMask> PersonMask { get; internal init; }

    /// <summary>Gets the identity binding all measurements to these source pixels.</summary>
    public string SourceSha256 => Image.SourceSha256;

    /// <summary>Gets the oriented source width.</summary>
    public int Width => Image.Width;

    /// <summary>Gets the oriented source height.</summary>
    public int Height => Image.Height;
}
