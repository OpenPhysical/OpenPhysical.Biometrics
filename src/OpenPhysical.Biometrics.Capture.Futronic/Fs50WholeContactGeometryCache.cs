// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class Fs50WholeContactGeometryCache
{
    private Maybe<(Fs50EmptySensorKey Key, Fs50WholeContactGeometry Geometry)> previous;

    internal Result<Fs50WholeContactGeometry, CaptureError> Get(
        Fs50EmptySensorKey key,
        CaptureGeometry geometry,
        ImmutableArray<byte> contact,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
        {
            Clear();
            return Fs50MaskErrors.Cancelled;
        }
        if (
            previous.HasValue
            && previous.Value.Key.SameSession(key)
            && previous.Value.Geometry.Contact.Mask.AsSpan().SequenceEqual(contact.AsSpan())
        )
            return previous.Value.Geometry;
        Clear();
        return Fs50WholeContactGeometry
            .Create(geometry, contact, token)
            .Map(value =>
            {
                previous = (key, value);
                return value;
            });
    }

    internal bool Clear()
    {
        previous = Maybe<(Fs50EmptySensorKey, Fs50WholeContactGeometry)>.None;
        return true;
    }
}
