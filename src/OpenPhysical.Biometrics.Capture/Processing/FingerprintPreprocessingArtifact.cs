// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;

/// <summary>Immutable original/processing association; derived pixels never replace acquisition provenance.</summary>
public sealed class FingerprintPreprocessingArtifact
{
    internal FingerprintPreprocessingArtifact(
        CaptureFrame original,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        ImmutableArray<byte> pixels,
        long revision
    )
    {
        OriginalFrame = original;
        Mask = mask;
        Policy = policy;
        Pixels = pixels;
        ProcessingRevision = revision;
        OriginalDigest = original.PixelDigest;
        ProcessedDigest = pixels.Equals(original.Pixels)
            ? OriginalDigest
            : CaptureProcessingDigest.Pixels(pixels.AsSpan());
        Source = FingerprintProcessingSource.From(original);
        Identity = FingerprintPreprocessingIdentity
            .Create(
                Source,
                OriginalDigest,
                ProcessedDigest,
                mask.Digest,
                policy.Digest,
                revision,
                Version
            )
            .Value;
    }

    /// <summary>Gets the unchanged acquisition and its exact provenance.</summary>
    public CaptureFrame OriginalFrame { get; }

    /// <summary>Gets validated source fields required for metadata-only identity verification.</summary>
    public FingerprintProcessingSource Source { get; }

    /// <summary>Gets the exact processing Gray8 bytes used for assessment and extraction.</summary>
    public ImmutableArray<byte> Pixels { get; }

    /// <summary>Gets the unchanged image width.</summary>
    public int Width => OriginalFrame.Width;

    /// <summary>Gets the unchanged image height.</summary>
    public int Height => OriginalFrame.Height;

    /// <summary>Gets the retained and guarded masks without recomputation.</summary>
    public FingerprintProcessingMask Mask { get; }

    /// <summary>Gets the snapshotted preprocessing settings.</summary>
    public FingerprintPreprocessingPolicy Policy { get; }

    /// <summary>Gets the preprocessing implementation identifier.</summary>
    public string Version => FingerprintPreprocessing.Version;

    /// <summary>Gets the operator processing revision.</summary>
    public long ProcessingRevision { get; }

    /// <summary>Gets the original Gray8 SHA-256.</summary>
    public string OriginalDigest { get; }

    /// <summary>Gets the processing Gray8 SHA-256.</summary>
    public string ProcessedDigest { get; }

    /// <summary>Gets the snapshotted policy SHA-256.</summary>
    public string PolicyDigest => Policy.Digest;

    /// <summary>Gets the source-, mask-, policy- and revision-bound artifact SHA-256.</summary>
    public string Identity { get; }
}
