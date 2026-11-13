// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

/// <summary>Owned facial image presentation and cached guide visibility, independent of capture commands.</summary>
public sealed class FaceImageViewModel : ObservableObject
{
    private readonly FaceFramePresentation display;
    private Maybe<PortraitAnalysis> analysis;
    private Maybe<PortraitPreparationResult> portrait;
    private bool sourceCoordinates;
    private bool live;
    private Maybe<FaceImageGuides> imageGuides;
    private Maybe<FaceImageGuides> detectionGuides;
    private bool showPiv = true;
    private bool showDetection;

    internal FaceImageViewModel(FaceFramePresentation display) => this.display = display;

    /// <summary>Gets the acknowledged preview or frozen portrait pixels.</summary>
    public Maybe<FacePreviewScene> Scene => display.Scene;

    /// <summary>Gets or sets whether cached portrait framing guides are shown.</summary>
    public bool ShowPivGuides
    {
        get => showPiv;
        set
        {
            if (SetProperty(ref showPiv, value))
                OnPropertyChanged(nameof(ImageGuides));
        }
    }

    /// <summary>Gets or sets whether cached face detections are shown.</summary>
    public bool ShowFaceDetection
    {
        get => showDetection;
        set
        {
            if (SetProperty(ref showDetection, value))
                OnPropertyChanged(nameof(DetectionGuides));
        }
    }

    /// <summary>Gets framing guides in the currently displayed image coordinates.</summary>
    public Maybe<FaceImageGuides> ImageGuides => showPiv && !live ? imageGuides : default;

    /// <summary>Gets face detections in the currently displayed image coordinates.</summary>
    public Maybe<FaceImageGuides> DetectionGuides => showDetection ? detectionGuides : default;

    internal UnitResult<DesktopError> Update(FaceCaptureWorkflow workflow)
    {
        OnPropertyChanged(nameof(Scene));
        var nextAnalysis = workflow.IsLive ? workflow.LiveAnalysis : workflow.Draft.Analysis;
        var nextPortrait = workflow.IsLive ? default : workflow.Draft.Portrait;
        var original =
            workflow.IsLive || !nextPortrait.Match(value => !value.RgbPixels.IsEmpty, () => false);
        if (
            Same(analysis, nextAnalysis)
            && Same(portrait, nextPortrait)
            && sourceCoordinates == original
            && live == workflow.IsLive
        )
            return UnitResult.Success<DesktopError>();
        analysis = nextAnalysis;
        portrait = nextPortrait;
        sourceCoordinates = original;
        live = workflow.IsLive;
        imageGuides = live
            ? default
            : portrait.Bind(value => FaceGuideFactory.Create(value, original));
        detectionGuides = portrait.Match(
            value => FaceGuideFactory.Detection(value, original),
            () => analysis.Bind(FaceGuideFactory.Detection)
        );
        OnPropertyChanged(nameof(ImageGuides));
        OnPropertyChanged(nameof(DetectionGuides));
        return UnitResult.Success<DesktopError>();
    }

    private static bool Same<T>(Maybe<T> first, Maybe<T> second)
        where T : class =>
        first.Match(
            value => second.Match(other => ReferenceEquals(value, other), () => false),
            () => second.HasNoValue
        );
}
