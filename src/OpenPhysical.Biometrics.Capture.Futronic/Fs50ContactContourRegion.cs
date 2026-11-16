// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Creates original-source-bound feature geometry without qualifying a physical presentation.</summary>
public static class Fs50ContactContourRegion
{
    /// <summary>Preserves full selected contact, fills only enclosed holes and builds separate image context.</summary>
    /// <param name="frame">Exact frozen corrected acquisition.</param>
    /// <param name="diagnostics">Contact evidence from that original acquisition.</param>
    /// <param name="policy">Validated image-context choices.</param>
    /// <param name="cancellationToken">Cancellation prevents publication of partial evidence.</param>
    /// <returns>Role-separated source-bound geometry or a precise source/input failure.</returns>
    public static Result<Fs50ContactContourArtifact, CaptureError> Create(
        CaptureFrame frame,
        Fs50ContactDiagnostics diagnostics,
        Fs50ContactContourPolicy policy,
        CancellationToken cancellationToken
    ) =>
        Fs50FrozenEvidence
            .Validate(
                frame,
                diagnostics,
                policy,
                Fs50ContactContourErrorCodes.SourceMismatch,
                cancellationToken
            )
            .Bind(() => Build(diagnostics, policy, cancellationToken));

    private static Result<Fs50ContactContourArtifact, CaptureError> Build(
        Fs50ContactDiagnostics diagnostics,
        Fs50ContactContourPolicy policy,
        CancellationToken token
    )
    {
        if (
            diagnostics.ContactMask.Length != 800 * 750
            || diagnostics.RidgeSupportMask.Length != 800 * 750
            || diagnostics.RidgeSupportMask.Any(value => value > 1)
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidFrame,
                "Contact contour requires complete binary source evidence."
            );
        var support = new byte[800 * 750];
        try
        {
            for (var i = 0; i < support.Length; i++)
                support[i] = (byte)(
                    diagnostics.ContactMask[i] != 0 && diagnostics.RidgeSupportMask[i] != 0 ? 1 : 0
                );
            return Fs50ContactContourGeometry
                .Create(diagnostics.ContactMask.AsSpan(), support, policy, token)
                .Map(shape => new Fs50ContactContourArtifact(
                    diagnostics.SourceDigest,
                    diagnostics.Version,
                    diagnostics.PolicyDigest,
                    shape
                ));
        }
        finally
        {
            Array.Clear(support);
        }
    }
}
