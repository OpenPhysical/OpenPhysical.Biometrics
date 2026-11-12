// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Operator-visible comparison choices, presentation state and decision status.</summary>
public abstract class InspectionStateViewModel : ObservableObject
{
    private string path = string.Empty;
    private Maybe<FingerprintScene> scene;
    private Maybe<FingerprintScene> probeScene;
    private bool referenceOverlay = true;
    private int threshold = 40;
    private string alignmentStatus = "Compare to align the reference points with this capture.";
    private bool overlay = true;
    private bool segmentation = true;
    private string summary = string.Empty;
    private string report = "Open a reference to inspect its finger views.";
    private FingerChoice assignedProbeFinger = FingerChoice.All.Single(choice =>
        choice.Position == FingerPosition.RightIndex
    );

    /// <summary>Creates comparison state bound to an actual capture source.</summary>
    /// <param name="capture">Observed capture state.</param>
    protected InspectionStateViewModel(ICapturedFingerprintSource capture) => Capture = capture;

    /// <summary>Gets the observed source used for probe rendering.</summary>
    protected ICapturedFingerprintSource Capture { get; }

    /// <summary>Gets whether late display changes have been stopped.</summary>
    protected bool DisplayStopped { get; set; }

    /// <summary>Gets or sets the reference file chosen by the operator.</summary>
    public string FilePath
    {
        get => path;
        set => SetProperty(ref path, value);
    }

    /// <summary>Gets the current match decision without inventing a decision before comparison.</summary>
    public ComparisonDecisionViewModel Decision { get; } = new();

    /// <summary>Gets or sets the verification-only finger assignment, preserving the captured source.</summary>
    public FingerChoice AssignedProbeFinger
    {
        get => assignedProbeFinger;
        set
        {
            if (!DisplayStopped && value is not null && SetProperty(ref assignedProbeFinger, value))
            {
                RefreshProbe();
                ResetDecision();
            }
        }
    }

    /// <summary>Gets a concise description of the current assigned probe.</summary>
    public string ProbeSummary =>
        InspectionProbeDescription.Describe(AssignedProbeFinger, Capture.Captured);

    /// <summary>Gets or sets the inclusive match threshold.</summary>
    public int Threshold
    {
        get => threshold;
        set
        {
            if (!DisplayStopped && SetProperty(ref threshold, value))
                ResetDecision();
        }
    }

    /// <summary>Gets the geometric registration status independently of the match decision.</summary>
    public string AlignmentStatus
    {
        get => alignmentStatus;
        protected set => SetProperty(ref alignmentStatus, value);
    }

    /// <summary>Gets the owned live or frozen probe scene.</summary>
    public Maybe<FingerprintScene> ProbeScene
    {
        get => probeScene;
        protected set => SetProperty(ref probeScene, value);
    }

    /// <summary>Gets or sets visibility of aligned reference minutiae on the probe.</summary>
    public bool ShowReferenceOverlay
    {
        get => referenceOverlay;
        set => SetProperty(ref referenceOverlay, value);
    }

    /// <summary>Gets the owned reference scene.</summary>
    public Maybe<FingerprintScene> Scene
    {
        get => scene;
        protected set => SetProperty(ref scene, value);
    }

    /// <summary>Gets the concise reference or comparison status.</summary>
    public string Summary
    {
        get => summary;
        protected set => SetProperty(ref summary, value);
    }

    /// <summary>Gets detailed reference or comparison information.</summary>
    public string Report
    {
        get => report;
        protected set => SetProperty(ref report, value);
    }

    /// <summary>Gets or sets reference point visibility.</summary>
    public bool ShowOverlay
    {
        get => overlay;
        set => SetProperty(ref overlay, value);
    }

    /// <summary>Gets or sets final region outline visibility.</summary>
    public bool ShowSegmentation
    {
        get => segmentation;
        set => SetProperty(ref segmentation, value);
    }

    /// <summary>Gets or sets the revision that rejects stale comparison completions.</summary>
    protected int DecisionRevision { get; set; }

    /// <summary>Refreshes the exact captured or live probe scene.</summary>
    /// <returns>Presentation success or its typed failure.</returns>
    protected abstract UnitResult<DesktopError> RefreshProbe();

    /// <summary>Invalidates a completed or pending comparison decision.</summary>
    /// <returns>Successful invalidation.</returns>
    protected abstract UnitResult<DesktopError> ResetDecision();
}
