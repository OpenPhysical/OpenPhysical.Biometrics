// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Frozen runtime image-admission evidence, never restorable from file metadata.</summary>
public sealed class Fs50WholeContactProof
{
    internal Fs50WholeContactProof(Fs50WholeContactRecord record) => Record = record;

    /// <summary>Gets persistable metadata that cannot recreate a live proof.</summary>
    public Fs50WholeContactRecord Record { get; }

    /// <summary>Gets whether current image evidence admits capture; the recorded decision distinguishes background comparison.</summary>
    public bool Qualified => Record.Qualified;

    /// <summary>Gets whether this exact source permits acquisition correction, independently of capture.</summary>
    public bool ExposureReady => Record.ExposureReady;

    /// <summary>Checks exact frozen-frame binding without consulting mutable connection state.</summary>
    /// <param name="frame">Original frozen acquisition, including its provenance.</param>
    /// <param name="contour">Original feature contour; current context edits may differ.</param>
    /// <returns>Original qualification or a precise association failure.</returns>
    public UnitResult<CaptureError> ValidateFor(
        CaptureFrame frame,
        Fs50ContactContourArtifact contour
    )
    {
        if (
            !Qualified
            || frame is null
            || frame.Sequence != Record.Identity.Sequence
            || frame.CapturedAt != Record.Identity.CapturedAt
            || frame.Provenance.Settings.Exposure != Record.Identity.Exposure
        )
            return Invalid();
        var key = Fs50EmptySensorKey.Create(frame, Record.Identity.SessionId);
        if (key.IsFailure)
            return key.Error;
        if (
            Fs50WholeContactIdentity.KeyDigest(key.Value) != Record.Identity.SessionKeyDigest
            || frame.PixelDigest != Record.Identity.SourceDigest
        )
            return Invalid();
        return Record.Validate(contour);
    }

    private static CaptureError Invalid() =>
        new(
            CaptureErrorCodes.InvalidFrame,
            "Live contact qualification is missing or belongs to another acquisition."
        );
}
