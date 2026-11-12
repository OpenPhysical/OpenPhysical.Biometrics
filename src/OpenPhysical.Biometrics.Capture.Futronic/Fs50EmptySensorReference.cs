// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;

/// <summary>Immutable session-only average of confidently empty acquisitions.</summary>
public sealed class Fs50EmptySensorReference
{
    internal Fs50EmptySensorReference(
        Fs50EmptySensorKey key,
        byte[] pixels,
        int frames,
        TimeSpan span
    )
    {
        Key = key;
        Pixels = ImmutableArray.CreateRange(pixels);
        Digest = Fs50PolicyDigest.Pixels(Pixels.AsSpan());
        Frames = frames;
        Span = span;
    }

    /// <summary>Gets the full reference compatibility key.</summary>
    public Fs50EmptySensorKey Key { get; }

    /// <summary>Gets RAM-only average pixels, never subtracted from a saved acquisition.</summary>
    public ImmutableArray<byte> Pixels { get; }

    /// <summary>Gets SHA-256 of the immutable RAM-only reference, without persisting its pixels.</summary>
    public string Digest { get; }

    /// <summary>Gets distinct stable acquisitions contributing to the average.</summary>
    public int Frames { get; }

    /// <summary>Gets the monotonic learning span.</summary>
    public TimeSpan Span { get; }
}
