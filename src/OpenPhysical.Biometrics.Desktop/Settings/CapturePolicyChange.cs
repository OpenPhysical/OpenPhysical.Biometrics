// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class CapturePolicyChange
{
    internal static Result<CapturePolicySettings, DesktopError> Apply(
        CapturePolicySettings current,
        EnhancementSettings enhancement,
        Fs50EvidenceOptions evidence,
        int gain,
        int margin
    ) =>
        CapturePolicySettings
            .Create(
                enhancement,
                evidence,
                current.EmptySensorOptions,
                current.MinimumNfiq1Gain,
                gain,
                margin
            )
            .Bind(settings => settings.WithPresentation(current.Presentation))
            .Bind(settings => settings.WithWholeContact(current.WholeContactOptions));
}
