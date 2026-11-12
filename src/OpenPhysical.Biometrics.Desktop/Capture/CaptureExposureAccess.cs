// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class CaptureExposureAccess(object sync, Func<bool> isLive)
{
    private readonly CaptureExposureState state = new();

    internal CaptureSettings Settings
    {
        get
        {
            lock (sync)
                return state.Settings;
        }
    }

    internal UnitResult<DesktopError> Configure(int exposure, bool automatic)
    {
        lock (sync)
            return state.Configure(exposure, automatic);
    }

    internal Result<bool, DesktopError> Adjust(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        Maybe<Fs50WholeContactProof> proof
    )
    {
        lock (sync)
            return state.Adjust(frame, contact, proof, isLive());
    }
}
