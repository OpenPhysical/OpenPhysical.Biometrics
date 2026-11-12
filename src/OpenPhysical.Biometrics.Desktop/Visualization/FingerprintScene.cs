// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using Avalonia.Media.Imaging;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>An owned rendering snapshot with canonical features and unchanged processing pixels.</summary>
/// <param name="Width">Source pixel width.</param>
/// <param name="Height">Source pixel height.</param>
/// <param name="Minutiae">Canonical exported features in source coordinates.</param>
/// <param name="Image">Optional owned display bitmap, disposed with this scene.</param>
public sealed record FingerprintScene(
    int Width,
    int Height,
    ImmutableArray<FingerprintMinutia> Minutiae,
    Maybe<WriteableBitmap> Image
) : IDisposable
{
    internal Maybe<FingerprintSceneFraming> Framing { get; init; }

    /// <summary>Gets the connected region outline in source pixel coordinates.</summary>
    public ImmutableArray<ForegroundEdge> Foreground { get; init; } = [];

    /// <summary>Gets the exact Gray8 processing samples behind the display bitmap.</summary>
    public ImmutableArray<byte> GrayPixels { get; init; } = [];

    /// <summary>Gets reference features aligned to this image for comparison.</summary>
    public ImmutableArray<FingerprintAlignedMinutia> ReferenceOverlay { get; init; } = [];

    /// <summary>Releases the owned display bitmap.</summary>
    public void Dispose() => Image.Tap(bitmap => bitmap.Dispose());
}
