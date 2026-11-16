// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Recomputes feature and context geometry from explicit or cached original contact evidence.</summary>
public static class Fs50ContactContourGeometry
{
    /// <summary>Preserves connected contact and inferred enclosed holes without ridge-support pruning.</summary>
    /// <param name="contact">Complete eight-connected binary selected contact C.</param>
    /// <param name="selectedSupport">Complete measured ridge-support plane S contained in C.</param>
    /// <param name="policy">Validated context choices.</param>
    /// <param name="cancellationToken">Cancellation prevents publication of partial geometry.</param>
    /// <returns>Immutable role-separated geometry or a precise invalid-input failure.</returns>
    public static Result<Fs50ContactContourShape, CaptureError> Create(
        ReadOnlySpan<byte> contact,
        ReadOnlySpan<byte> selectedSupport,
        Fs50ContactContourPolicy policy,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
            return Fs50MaskErrors.Cancelled;
        if (
            policy is null
            || contact.Length != 800 * 750
            || selectedSupport.Length != contact.Length
        )
            return Invalid();
        for (var i = 0; i < contact.Length; i++)
            if (contact[i] > 1 || selectedSupport[i] > contact[i])
                return Invalid();
        var owned = contact.ToArray();
        var support = selectedSupport.ToArray();
        try
        {
            return Fs50ContourTopology
                .Connected(owned)
                .Bind(() => Fs50ContourTopology.Fill(owned, cancellationToken))
                .Bind(feature => Build(owned, support, feature, policy, cancellationToken));
        }
        finally
        {
            Array.Clear(owned);
            Array.Clear(support);
        }
    }

    private static Result<Fs50ContactContourShape, CaptureError> Build(
        byte[] contact,
        byte[] support,
        byte[] feature,
        Fs50ContactContourPolicy policy,
        CancellationToken token
    )
    {
        try
        {
            return Fs50SupportHull
                .Create(contact, token)
                .Bind(vertices =>
                    Fs50HullRasterizer
                        .Draw(vertices, token)
                        .Bind(hull =>
                            Complete(
                                contact,
                                support,
                                feature,
                                hull,
                                policy,
                                vertices.Length,
                                token
                            )
                        )
                );
        }
        finally
        {
            Array.Clear(feature);
        }
    }

    internal static Result<Fs50ContactContourShape, CaptureError> Recontextualize(
        Fs50ContactContourShape shape,
        Fs50ContactContourPolicy policy,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
            return Fs50MaskErrors.Cancelled;
        if (policy is null)
            return Invalid();
        var contact = shape.ContactMask.ToArray();
        var support = shape.SupportMask.ToArray();
        var feature = shape.FeatureMask.ToArray();
        try
        {
            return Build(contact, support, feature, policy, token);
        }
        finally
        {
            Array.Clear(contact);
            Array.Clear(support);
        }
    }

    private static Result<Fs50ContactContourShape, CaptureError> Complete(
        byte[] contact,
        byte[] support,
        byte[] feature,
        byte[] hull,
        Fs50ContactContourPolicy policy,
        int vertexCount,
        CancellationToken token
    )
    {
        var context = Fs50MaskMorphology.Expand(hull, policy.Options.ContextMargin);
        try
        {
            return token.IsCancellationRequested
                ? Fs50MaskErrors.Cancelled
                : new Fs50ContactContourShape(
                    policy,
                    contact,
                    support,
                    feature,
                    context,
                    vertexCount
                );
        }
        finally
        {
            Array.Clear(hull);
            Array.Clear(context);
        }
    }

    private static CaptureError Invalid() =>
        new(
            CaptureErrorCodes.InvalidArgument,
            "Contact and selected support must be complete binary FS50 planes with support inside contact."
        );
}
