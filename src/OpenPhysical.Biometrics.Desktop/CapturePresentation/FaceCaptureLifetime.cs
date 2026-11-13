// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal sealed class FaceCaptureLifetime(
    FaceCaptureWorkflow workflow,
    FaceReviewOperations review,
    FaceCaptureCommands actions
)
{
    private readonly FacePhotoPolicyOperations policies = review.Policy(actions);
    internal bool Stopped { get; private set; }

    internal Task<UnitResult<DesktopError>> ConfigureMouthPolicyAsync(PortraitMouthPolicy policy) =>
        policies.ConfigureMouthAsync(policy);

    internal Task<UnitResult<DesktopError>> ConfigureBackgroundAsync(bool enabled) =>
        policies.ConfigureBackgroundAsync(enabled);

    internal Task<UnitResult<DesktopError>> ActivateAsync(bool active) =>
        Stopped
            ? Task.FromResult(UnitResult.Success<DesktopError>())
            : workflow.ActivateAsync(active);

    internal async Task<UnitResult<DesktopError>> ResetAsync()
    {
        workflow.Automatic.Reset();
        await policies.CancelAsync(false);
        review.CancelWork();
        await DesktopCommandTasks.DrainAsync(actions.Capture, actions.Accept);
        return await workflow.ResetAsync();
    }

    internal async Task<UnitResult<DesktopError>> StopAsync()
    {
        Stopped = true;
        workflow.Automatic.Reset();
        await policies.CancelAsync(true);
        review.CancelWork();
        await DesktopCommandTasks.DrainAsync(actions.All.ToArray());
        return await workflow.StopAsync();
    }
}
