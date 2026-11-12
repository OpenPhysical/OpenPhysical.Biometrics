// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record CaptureSensorObservation(
    Fs50ContactRegion Contact,
    bool Ready,
    Maybe<Fs50WholeContactProof> WholeContactProof,
    bool ConfidentlyEmpty
)
{
    internal bool ExposureReady =>
        WholeContactProof.Match(value => value.ExposureReady, () => false);
}
