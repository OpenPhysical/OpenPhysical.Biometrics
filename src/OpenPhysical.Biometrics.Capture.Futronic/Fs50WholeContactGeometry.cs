// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class Fs50WholeContactGeometry
{
    private Fs50WholeContactGeometry(CaptureRegionGeometry contact, CaptureRegionGeometry feature)
    {
        Contact = contact;
        Feature = feature;
        ContactDigest = Fs50PolicyDigest.Pixels(contact.Mask.AsSpan());
        FeatureDigest = contact.Mask.AsSpan().SequenceEqual(feature.Mask.AsSpan())
            ? ContactDigest
            : Fs50PolicyDigest.Pixels(feature.Mask.AsSpan());
    }

    internal CaptureRegionGeometry Contact { get; }
    internal CaptureRegionGeometry Feature { get; }
    internal string ContactDigest { get; }
    internal string FeatureDigest { get; }

    internal static Result<Fs50WholeContactGeometry, CaptureError> Create(
        CaptureGeometry geometry,
        ImmutableArray<byte> mask,
        CancellationToken token
    )
    {
        var contact = CaptureRegionGeometry.Create(geometry, mask);
        if (contact.IsFailure)
            return contact.Error;
        if (contact.Value.Area == 0)
            return new Fs50WholeContactGeometry(contact.Value, contact.Value);
        var filled = Fs50ContourTopology.Fill(mask.AsSpan(), token);
        if (filled.IsFailure)
            return filled.Error;
        try
        {
            return CaptureRegionGeometry
                .Create(geometry, filled.Value.AsSpan())
                .Map(feature => new Fs50WholeContactGeometry(contact.Value, feature));
        }
        finally
        {
            Array.Clear(filled.Value);
        }
    }
}
