// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class CaptureContactLoss(TimeProvider clock)
{
    private string key = string.Empty;
    private ImmutableArray<byte> footprint = [];
    private int supported,
        area,
        spatialArea,
        dark,
        absent;
    private double threshold;
    private Maybe<long> previous;
    private long sequence = -1;
    internal TimeSpan MaximumGap { get; set; } = TimeSpan.FromSeconds(1);

    internal UnitResult<DesktopError> Remember(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        Fs50WholeContactProof proof
    )
    {
        key = proof.Record.Identity.SessionKeyDigest;
        footprint = contact.ContactMask;
        supported = contact.Diagnostics.RidgeSupportArea;
        area = contact.Diagnostics.AcceptedArea;
        spatialArea = contact.SpatialStatistics.Match(value => value.Area, () => 0);
        threshold = 255 - contact.Diagnostics.AcceptedStatistics.Signal / 2;
        dark = DarkPixels(frame);
        return Interrupt();
    }

    internal Maybe<bool> Observe(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        Fs50WholeContactProof proof
    )
    {
        var lost =
            key.Length > 0
            && key == proof.Record.Identity.SessionKeyDigest
            && supported > 0
            && area > 0
            && spatialArea > 0
            && dark > 0
            && contact.Diagnostics.SourceDigest == frame.PixelDigest
            && contact.Diagnostics.RidgeSupportArea <= supported * .1
            && contact.Diagnostics.AcceptedArea <= area * .1
            // Weak coherent ridges remain present even when darkness-based acceptance collapses.
            && contact.SpatialStatistics.Match(value => value.Area, () => 0) <= spatialArea * .1
            && DarkPixels(frame) <= dark * .1;
        if (!lost)
        {
            Interrupt();
            return default;
        }
        if (frame.Sequence == sequence)
            return absent >= 2;
        var now = clock.GetTimestamp();
        if (
            frame.Sequence < sequence
            || previous.Match(value => clock.GetElapsedTime(value, now) > MaximumGap, () => false)
        )
            Interrupt();
        sequence = frame.Sequence;
        previous = now;
        absent = Math.Min(2, absent + 1);
        return absent >= 2;
    }

    internal UnitResult<DesktopError> Interrupt()
    {
        absent = 0;
        sequence = -1;
        previous = default;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Clear()
    {
        key = string.Empty;
        footprint = [];
        supported = area = spatialArea = dark = 0;
        return Interrupt();
    }

    private int DarkPixels(CaptureFrame frame)
    {
        if (footprint.Length != frame.Pixels.Length)
            return int.MaxValue;
        var count = 0;
        for (var index = 0; index < footprint.Length; index++)
            if (footprint[index] != 0 && frame.Pixels[index] < threshold)
                count++;
        return count;
    }
}
