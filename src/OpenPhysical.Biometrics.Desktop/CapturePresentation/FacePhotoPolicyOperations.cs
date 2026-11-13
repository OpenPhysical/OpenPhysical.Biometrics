// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal sealed class FacePhotoPolicyOperations(
    FaceCaptureWorkflow workflow,
    FaceCaptureProcessing processor,
    FaceCaptureCommands actions
)
{
    private int revision;
    private bool stopped;
    private bool failed;
    private Task<UnitResult<DesktopError>> pending = Task.FromResult(
        UnitResult.Success<DesktopError>()
    );

    internal Task<UnitResult<DesktopError>> ConfigureMouthAsync(PortraitMouthPolicy policy)
    {
        if (stopped || (processor.MouthPolicy == policy && !failed))
            return pending;
        processor.MouthPolicy = policy;
        return Schedule(false);
    }

    internal Task<UnitResult<DesktopError>> ConfigureBackgroundAsync(bool enabled)
    {
        if (stopped || processor.BackgroundPolicy.Enabled == enabled && !failed)
            return pending;
        processor.BackgroundPolicy = processor.BackgroundPolicy with { Enabled = enabled };
        return Schedule(true);
    }

    private Task<UnitResult<DesktopError>> Schedule(bool render)
    {
        workflow.Automatic.Reset();
        failed = false;
        if (workflow.Draft.Accepted.HasNoValue)
            workflow.Draft.ClearReviewEvidence();
        pending = ApplyAsync(
            ++revision,
            render,
            processor.MouthPolicy,
            processor.BackgroundPolicy,
            pending
        );
        return pending;
    }

    private async Task<UnitResult<DesktopError>> ApplyAsync(
        int expected,
        bool render,
        PortraitMouthPolicy mouth,
        PortraitBackgroundPolicy background,
        Task<UnitResult<DesktopError>> previous
    )
    {
        await previous;
        await DesktopCommandTasks.DrainAsync(actions.Capture, actions.Accept);
        var draft = workflow.Draft;
        if (
            expected != revision
            || stopped
            || draft.Accepted.HasValue
            || draft.Portrait.HasNoValue
            || draft.Portrait.Value.RgbPixels.IsEmpty
        )
            return UnitResult.Success<DesktopError>();
        var snapshot = FacePolicySnapshot.From(draft);
        render |= background.Enabled != snapshot.Portrait.Background.HasValue;
        workflow.SetBusy(true);
        var result = await Task.Run(() => Reprocess(snapshot, render, mouth, background));
        if (expected != revision || stopped || draft.Accepted.HasValue || !snapshot.Matches(draft))
            return UnitResult.Success<DesktopError>();
        workflow.SetBusy(false);
        failed = result.IsFailure;
        return result.IsFailure
            ? workflow.Fail(result.Error)
            : draft
                .ReplaceProcessing(result.Value.Analysis, result.Value.Review)
                .Bind(() => render ? workflow.ShowDraft() : UnitResult.Success<DesktopError>())
                .Bind(() => workflow.Guide("Review the portrait before accepting the face."));
    }

    private Result<(PortraitAnalysis Analysis, FacePreparedReview Review), DesktopError> Reprocess(
        FacePolicySnapshot snapshot,
        bool render,
        PortraitMouthPolicy mouth,
        PortraitBackgroundPolicy background
    ) =>
        render
            ? processor
                .PrepareFrozen(snapshot.Analysis, background)
                .Bind(analysis =>
                    processor
                        .PrepareReview(analysis, mouth, background)
                        .Map(review => (analysis, review))
                )
            : processor
                .Reassess(snapshot.Portrait, mouth)
                .Map(portrait =>
                    (snapshot.Analysis, new FacePreparedReview(portrait, snapshot.Quality))
                );

    internal async Task<UnitResult<DesktopError>> CancelAsync(bool stop)
    {
        revision++;
        stopped |= stop;
        var previous = pending;
        await previous;
        if (ReferenceEquals(previous, pending))
        {
            failed = false;
            pending = Task.FromResult(UnitResult.Success<DesktopError>());
        }
        return workflow.SetBusy(false);
    }
}
