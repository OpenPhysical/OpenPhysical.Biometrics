// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class Fs50WholeContactGeometryPairCache
{
    private readonly Fs50WholeContactGeometryCache contact = new();
    private readonly Fs50WholeContactGeometryCache observed = new();

    internal Result<
        (Fs50WholeContactGeometry Contact, Fs50WholeContactGeometry Observed),
        CaptureError
    > Get(
        Fs50EmptySensorKey key,
        CaptureGeometry geometry,
        ImmutableArray<byte> contactMask,
        ImmutableArray<byte> observedMask,
        CancellationToken token
    ) =>
        contact
            .Get(key, geometry, contactMask, token)
            .Bind(c => observed.Get(key, geometry, observedMask, token).Map(w => (c, w)));

    internal bool Clear()
    {
        contact.Clear();
        return observed.Clear();
    }
}
