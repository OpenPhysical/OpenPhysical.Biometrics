// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Immutable contact geometry with inferred holes and independent image context.</summary>
public sealed class Fs50ContactContourShape
{
    internal Fs50ContactContourShape(
        Fs50ContactContourPolicy policy,
        byte[] contact,
        byte[] support,
        byte[] feature,
        byte[] context,
        int vertices
    )
    {
        Policy = policy;
        var holes = new byte[contact.Length];
        var uncertain = new byte[contact.Length];
        for (var i = 0; i < contact.Length; i++)
        {
            holes[i] = (byte)(feature[i] != 0 && contact[i] == 0 ? 1 : 0);
            uncertain[i] = (byte)(feature[i] != 0 && support[i] == 0 ? 1 : 0);
        }
        Planes = new(
            [.. contact],
            [.. holes],
            [.. feature],
            [.. support],
            [.. context],
            [.. uncertain]
        );
        Statistics = Count(Planes, vertices);
        ContactDigest = Fs50PolicyDigest.Pixels(contact);
        FilledHoleDigest = Fs50PolicyDigest.Pixels(holes);
        FeatureDigest = Fs50PolicyDigest.Pixels(feature);
        SupportDigest = Fs50PolicyDigest.Pixels(support);
        ContextDigest = Fs50PolicyDigest.Pixels(context);
        UncertainDigest = Fs50PolicyDigest.Pixels(uncertain);
        Array.Clear(holes);
        Array.Clear(uncertain);
    }

    /// <summary>Gets the validated context choices.</summary>
    public Fs50ContactContourPolicy Policy { get; }

    /// <summary>Gets all independently named immutable planes.</summary>
    public Fs50ContactContourPlanes Planes { get; }

    /// <summary>Gets original measured connected contact C.</summary>
    public ImmutableArray<byte> ContactMask => Planes.ContactMask;

    /// <summary>Gets inferred enclosed uncertainty H.</summary>
    public ImmutableArray<byte> FilledHoleMask => Planes.FilledHoleMask;

    /// <summary>Gets spatial feature admission F, independent of context margin.</summary>
    public ImmutableArray<byte> FeatureMask => Planes.FeatureMask;

    /// <summary>Gets measured selected ridge support S.</summary>
    public ImmutableArray<byte> SupportMask => Planes.SupportMask;

    /// <summary>Gets full-contact convex image context E.</summary>
    public ImmutableArray<byte> ContextMask => Planes.ContextMask;

    /// <summary>Gets F minus S, without relabeling it measured support.</summary>
    public ImmutableArray<byte> UncertainMask => Planes.UncertainMask;

    /// <summary>Gets SHA-256 of C.</summary>
    public string ContactDigest { get; }

    /// <summary>Gets SHA-256 of H.</summary>
    public string FilledHoleDigest { get; }

    /// <summary>Gets SHA-256 of F.</summary>
    public string FeatureDigest { get; }

    /// <summary>Gets SHA-256 of S.</summary>
    public string SupportDigest { get; }

    /// <summary>Gets SHA-256 of E.</summary>
    public string ContextDigest { get; }

    /// <summary>Gets SHA-256 of F minus S.</summary>
    public string UncertainDigest { get; }

    /// <summary>Gets geometry counts without a physical-contact assertion.</summary>
    public Fs50ContactContourStatistics Statistics { get; }

    /// <summary>Separates pixel context from actual feature admission and modified-context cuts.</summary>
    /// <param name="modifiedPixels">Whether excluded context pixels will be modified.</param>
    /// <returns>Pixel and feature masks with the actual applied cut guard.</returns>
    public Result<Fs50ContourProcessingRegion, CaptureError> ToProcessingRegion(
        bool modifiedPixels
    ) => Fs50ContourProcessingRegion.Create(this, modifiedPixels);

    /// <summary>Changes context choices while preserving the exact original contact, holes and feature planes.</summary>
    /// <param name="policy">New validated context choices.</param>
    /// <param name="cancellationToken">Cancellation prevents publication of partial geometry.</param>
    /// <returns>Geometry with unchanged feature admission and updated image context.</returns>
    public Result<Fs50ContactContourShape, CaptureError> WithContext(
        Fs50ContactContourPolicy policy,
        CancellationToken cancellationToken
    ) => Fs50ContactContourGeometry.Recontextualize(this, policy, cancellationToken);

    private static Fs50ContactContourStatistics Count(
        Fs50ContactContourPlanes planes,
        int vertices
    ) =>
        new(
            planes.ContactMask.Sum(value => value),
            planes.FilledHoleMask.Sum(value => value),
            planes.FeatureMask.Sum(value => value),
            planes.SupportMask.Sum(value => value),
            planes.ContextMask.Sum(value => value),
            planes.UncertainMask.Sum(value => value),
            vertices
        );
}
