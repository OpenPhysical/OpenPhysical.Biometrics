// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class FaceReviewOperations(
    FaceCaptureWorkflow workflow,
    FaceCaptureProcessing processor,
    Func<UnitResult<DesktopError>> changed
)
{
    private int version;

    internal FacePhotoPolicyOperations Policy(FaceCaptureCommands commands) =>
        new(workflow, processor, commands);

    internal UnitResult<DesktopError> Confirm(bool value)
    {
        workflow.Draft.ReviewConfirmed = value;
        return changed();
    }

    internal bool CanAccept(bool identityReady) =>
        !workflow.Busy
        && workflow.Draft.Accepted.HasNoValue
        && workflow.Draft.ReviewConfirmed
        && !workflow.Draft.RequiresRetake
        && identityReady
        && workflow.Draft.Overrides.Ready(workflow.Draft.Portrait);

    internal bool CanOverride =>
        !workflow.Busy
        && workflow.Draft.Accepted.HasNoValue
        && workflow.Draft.Overrides.CanCreate(workflow.Draft.Portrait);

    internal Task OverrideAsync()
    {
        if (!CanOverride)
            return Task.CompletedTask;
        var applied = workflow.Draft.Overrides.Apply(workflow.Draft.Portrait);
        if (applied.IsFailure)
            workflow.Fail(applied.Error);
        else
        {
            workflow.Draft.ReviewConfirmed = false;
            workflow.Guide("Override recorded. Review the portrait before accepting.");
        }
        return Task.CompletedTask;
    }

    internal async Task AcceptAsync()
    {
        var input = processor.Snapshot(workflow.Draft, workflow.Selection.OpticalQualification);
        if (input.IsFailure)
        {
            workflow.Fail(input.Error);
            return;
        }
        var expected = ++version;
        workflow.SetBusy(true);
        var accepted = await Task.Run(() => processor.Accept(input.Value));
        if (expected != version)
            return;
        workflow.SetBusy(false);
        if (accepted.IsFailure)
        {
            workflow.Fail(accepted.Error);
            return;
        }
        workflow.Draft.Accepted = accepted.Value;
        changed();
    }

    internal UnitResult<DesktopError> CancelWork()
    {
        version++;
        return workflow.SetBusy(false);
    }
}
