// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal sealed class CaptureAttemptSelection(
    CaptureCoordinator coordinator,
    CaptureAutomation automation
)
{
    internal Result<(CaptureFrame Frame, CaptureWorkRequest Request), DesktopError> Snapshot(
        (FingerprintPreprocessingPolicy Policy, long Revision) image
    )
    {
        var selected = automation.TakeSelection();
        var candidates = automation.TakeCandidates();
        automation.Reset(true);
        return coordinator
            .SnapshotWork(selected.Map(value => value.Frame), candidates)
            .Map(snapshot =>
                (
                    snapshot.Frame,
                    CaptureWorkSnapshot.Create(
                        snapshot.Candidates,
                        image,
                        coordinator.Policy,
                        coordinator.AutoCaptureDelay,
                        snapshot.Input
                    )
                )
            );
    }
}
