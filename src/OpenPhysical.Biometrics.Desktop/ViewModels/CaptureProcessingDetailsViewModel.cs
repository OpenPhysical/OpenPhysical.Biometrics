// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Exposes diagnostic image stages without recomputing stored processing evidence.</summary>
public sealed class CaptureProcessingDetailsViewModel : ObservableObject
{
    private Maybe<CaptureProcessingDetailsSource> source;
    private string selected = "Processing image";
    private Maybe<FingerprintScene> scene;
    private bool open;

    /// <summary>Gets diagnostic stages available for inspection.</summary>
    public ImmutableArray<string> Stages { get; } =
    [
        "Original",
        "Candidate mask",
        "Ridge support",
        "Measured contact",
        "Filled holes",
        "Feature area",
        "Selected ridge support",
        "Uncertain area",
        "Image context",
        "Guarded context",
        "Eligible features",
        "Rejected area",
        "Processing image",
    ];

    /// <summary>Gets whether observation evidence is available.</summary>
    public bool Available => source.HasValue;

    /// <summary>Gets or sets the selected diagnostic stage.</summary>
    public string Selected
    {
        get => selected;
        set
        {
            if (Stages.Contains(value) && SetProperty(ref selected, value))
            {
                OnPropertyChanged(nameof(Description));
                if (open)
                    Refresh();
            }
        }
    }

    /// <summary>Gets the selected diagnostic scene while its viewer is open.</summary>
    public Maybe<FingerprintScene> Scene
    {
        get => scene;
        private set => SetProperty(ref scene, value);
    }

    /// <summary>Gets the selected stage evidence and provenance description.</summary>
    public string Description =>
        source.Match(value => CaptureDetailsDescription.For(value, Selected), () => string.Empty);

    /// <summary>Displays the immutable processing evidence belonging to a prepared capture.</summary>
    /// <param name="capture">Prepared or reopened capture whose evidence should be inspected.</param>
    /// <returns>Evidence presentation outcome.</returns>
    public UnitResult<DesktopError> UpdateCapture(CapturedFingerprint capture) =>
        UpdateSource(
            capture.Frame,
            capture.Processing.Map(value => value.Artifact),
            capture.Processing.Map(value => value.ContactDiagnostics),
            capture.Processing
        );

    internal UnitResult<DesktopError> UpdateSource(
        CaptureFrame original,
        Maybe<FingerprintPreprocessingArtifact> processing,
        Maybe<Fs50ContactDiagnostics> diagnostics,
        Maybe<CaptureProcessingEvidence> evidence = default
    )
    {
        if (processing.HasValue && !ReferenceEquals(processing.Value.OriginalFrame, original))
        {
            Clear();
            return UnitResult.Failure(
                new DesktopError(
                    "Capture.DetailsSourceMismatch",
                    "The diagnostic images do not belong to this observation."
                )
            );
        }
        source = new CaptureProcessingDetailsSource(original, processing, diagnostics, evidence);
        OnPropertyChanged(nameof(Available));
        OnPropertyChanged(nameof(Description));
        return open ? Refresh() : ClearScene();
    }

    /// <summary>Opens or closes the stage viewer and releases unused image resources.</summary>
    /// <param name="value">Whether the viewer is visible.</param>
    /// <returns>Viewer update outcome.</returns>
    public UnitResult<DesktopError> SetOpen(bool value)
    {
        open = value;
        return value ? Refresh() : ClearScene();
    }

    internal UnitResult<DesktopError> Clear()
    {
        source = Maybe<CaptureProcessingDetailsSource>.None;
        OnPropertyChanged(nameof(Available));
        OnPropertyChanged(nameof(Description));
        return ClearScene();
    }

    private UnitResult<DesktopError> ClearScene()
    {
        Scene = SceneLifetime.Clear(scene);
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> Refresh()
    {
        ClearScene();
        var result = source.Match(
            value => CaptureProcessingStages.Scene(value, Selected),
            () =>
                Result.Success<Maybe<FingerprintScene>, DesktopError>(Maybe<FingerprintScene>.None)
        );
        if (result.IsSuccess)
            Scene = result.Value;
        return result.IsSuccess
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(result.Error);
    }
}
