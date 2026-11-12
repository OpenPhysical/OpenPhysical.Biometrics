// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class CaptureExposureState
{
    private byte exposure = DesktopCaptureDefaults.Exposure;
    private bool automatic;
    internal CaptureSettings Settings =>
        CaptureSettings.Create(exposure, false, true, TimeSpan.FromMinutes(5)).Value;

    internal UnitResult<DesktopError> Configure(int next, bool enabled)
    {
        if (next is < 0 or > 255)
            return new DesktopError("Capture.Exposure", "Exposure must be between 0 and 255.");
        exposure = (byte)next;
        automatic = enabled;
        return UnitResult.Success<DesktopError>();
    }

    internal Result<bool, DesktopError> Adjust(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        Maybe<Fs50WholeContactProof> proof,
        bool live
    )
    {
        if (
            !automatic
            || !live
            || exposure != frame.Provenance.Settings.Exposure
            || proof.HasNoValue
            || !proof.Value.ExposureReady
            || contact.SpatialStatistics.HasNoValue
        )
            return false;
        if (!SameSource(frame, contact, proof.Value.Record.Identity))
            return new DesktopError(
                "Capture.ExposureSourceMismatch",
                "Exposure evidence belongs to another acquisition."
            );
        var next = Fs50ExposureController.NextObserved(
            exposure,
            contact.SpatialStatistics.Value,
            true,
            false,
            DesktopCaptureDefaults.DarknessTarget
        );
        if (next.IsFailure)
            return new DesktopError(next.Error.Code, next.Error.Message);
        var changed = exposure != next.Value;
        exposure = next.Value;
        return changed;
    }

    private bool SameSource(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        Fs50WholeContactIdentity identity
    ) =>
        identity.SourceDigest == frame.PixelDigest
        && identity.Sequence == frame.Sequence
        && identity.CapturedAt == frame.CapturedAt
        && identity.Exposure == exposure
        && contact.Diagnostics.SourceDigest == identity.SourceDigest;
}
