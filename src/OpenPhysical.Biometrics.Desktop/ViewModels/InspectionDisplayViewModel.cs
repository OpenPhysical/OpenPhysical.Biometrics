// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.ComponentModel;
using CSharpFunctionalExtensions;

/// <summary>Renders reference and probe state and owns their display lifetime.</summary>
public abstract class InspectionDisplayViewModel : InspectionStateViewModel
{
    private readonly PropertyChangedEventHandler captureChanged;

    /// <summary>Observes live and frozen capture changes without transferring scene ownership.</summary>
    /// <param name="capture">Actual capture source.</param>
    protected InspectionDisplayViewModel(ICapturedFingerprintSource capture)
        : base(capture)
    {
        captureChanged = (_, change) =>
        {
            if (
                !DisplayStopped
                && change.PropertyName
                    is nameof(ICapturedFingerprintSource.Captured)
                        or nameof(ICapturedFingerprintSource.LiveFrame)
            )
            {
                RefreshProbe();
                ResetDecision();
            }
        };
        Capture.PropertyChanged += captureChanged;
        RefreshProbe();
    }

    /// <inheritdoc/>
    protected override UnitResult<DesktopError> RefreshProbe()
    {
        AlignmentStatus = "Compare to align the reference points with this Capture.";
        if (Capture.Captured.HasNoValue)
        {
            if (Capture.LiveFrame.HasValue)
            {
                return PresentProbe(DisplayedSceneFactory.Create(Capture.LiveFrame.Value));
            }
            ProbeScene = SceneLifetime.Clear(ProbeScene);
            return UnitResult.Success<DesktopError>();
        }
        var value = Capture.Captured.Value;
        return PresentProbe(SceneFactory.Captured(value, false));
    }

    /// <summary>Replaces the owned probe image with a rendered scene.</summary>
    /// <param name="rendered">Rendered scene or its failure.</param>
    /// <returns>Presentation success or its typed failure.</returns>
    protected UnitResult<DesktopError> PresentProbe(Result<FingerprintScene, DesktopError> rendered)
    {
        if (rendered.IsFailure)
        {
            ProbeScene = SceneLifetime.Clear(ProbeScene);
            return UnitResult.Failure(rendered.Error);
        }
        ProbeScene = SceneLifetime.Replace(ProbeScene, rendered.Value);
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Clears owned scenes and the visible decision.</summary>
    /// <returns>Successful clearing.</returns>
    protected UnitResult<DesktopError> StopDisplay()
    {
        Decision.Clear();
        ProbeScene = SceneLifetime.Clear(ProbeScene);
        Scene = SceneLifetime.Clear(Scene);
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Clears the probe presentation for a new person.</summary>
    /// <returns>Successful clearing.</returns>
    protected UnitResult<DesktopError> ClearPersonProbe()
    {
        ProbeScene = SceneLifetime.Clear(ProbeScene);
        AlignmentStatus = "Choose a reference fingerprint to begin.";
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Presents one actual selected reference view.</summary>
    /// <param name="document">Loaded reference document when available.</param>
    /// <param name="selected">Actual record view index.</param>
    /// <returns>Presentation success or a typed selection/render failure.</returns>
    protected UnitResult<DesktopError> ShowReference(Maybe<TemplateDocument> document, int selected)
    {
        if (DisplayStopped)
            return UnitResult.Success<DesktopError>();
        var rendered = InspectionPresentation.Render(document, selected);
        if (rendered.IsFailure)
        {
            Scene = SceneLifetime.Clear(Scene);
            Report = $"{rendered.Error.Message} ({rendered.Error.Code})";
            Summary = rendered.Error.Message;
            return UnitResult.Failure(rendered.Error);
        }
        Scene = SceneLifetime.Replace(Scene, rendered.Value);
        document.Tap(value => Report = TemplateInspection.DescribeReference(value.Record));
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Rejects late display changes and unsubscribes from capture updates.</summary>
    /// <returns>Successful lifetime invalidation.</returns>
    protected UnitResult<DesktopError> BeginStopDisplay()
    {
        DisplayStopped = true;
        Capture.PropertyChanged -= captureChanged;
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Publishes an actual comparison failure while retaining the probe image.</summary>
    /// <param name="error">Actual comparison failure.</param>
    /// <returns>Probe presentation result.</returns>
    protected UnitResult<DesktopError> FailedComparison(DesktopError error)
    {
        Decision.Clear();
        Summary = error.Message;
        Report = $"{error.Message} ({error.Code})";
        return RefreshProbe();
    }

    /// <inheritdoc/>
    protected override UnitResult<DesktopError> ResetDecision()
    {
        Decision.Clear();
        DecisionRevision++;
        OnPropertyChanged(nameof(ProbeSummary));
        Summary = Capture.Captured.HasValue
            ? $"{AssignedProbeFinger.Label} ready to compare."
            : string.Empty;
        Report = Capture.Captured.HasValue
            ? "Compare the captured fingerprint with the selected reference view."
            : "No frozen Capture. Capture a finger before comparing.";
        return UnitResult.Success<DesktopError>();
    }

    private protected UnitResult<DesktopError> PublishComparison(
        InspectionComparisonRequest request,
        InspectionComparisonOutput output
    )
    {
        Decision.Set(output.Accepted);
        Summary = output.Summary;
        Report = output.Report;
        var rendered = output.Alignment.Match(
            alignment => PresentProbe(SceneFactory.Aligned(request.Probe, alignment)),
            RefreshProbe
        );
        AlignmentStatus = output.AlignmentStatus;
        if (rendered.IsFailure)
            Report = $"{Report}\n{rendered.Error.Message} ({rendered.Error.Code})";
        return rendered;
    }
}
