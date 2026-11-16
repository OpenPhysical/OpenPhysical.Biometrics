// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

/// <summary>Prepares source-supported PIV Full Frontal portraits using MediaPipe geometry.</summary>
/// <remarks>Calls are serialized with disposal. Returned buffers are immutable and owned by the result.</remarks>
public sealed class PortraitPreparer : IDisposable
{
    private readonly object gate = new();
    private readonly Lazy<Result<MediaPipeDetector, FacialError>> detector = new(
        MediaPipeDetector.Create
    );
    private readonly ILogger<PortraitPreparer> logger;
    private readonly PortraitBackgroundPreparation background;
    private bool disposed;

    /// <summary>Creates a portrait service whose model sessions initialize on the first call.</summary>
    /// <param name="logger">Status-only logger.</param>
    public PortraitPreparer(ILogger<PortraitPreparer> logger)
    {
        this.logger = logger;
        background = new(logger);
    }

    /// <summary>Enriches one frozen source with reusable person confidence, without changing its pixels.</summary>
    /// <param name="analysis">Exact cached source detection selected for capture.</param>
    /// <returns>The immutable enriched analysis, or a typed segmentation failure.</returns>
    public Result<PortraitAnalysis, FacialError> PrepareBackground(PortraitAnalysis analysis)
    {
        lock (gate)
        {
            if (disposed)
                return FacialErrors.Disposed().Fail<PortraitAnalysis>();
            return background.Prepare(analysis);
        }
    }

    /// <summary>Decodes, detects, and prepares a portrait of a single face without reviewed geometry.</summary>
    /// <param name="encodedImage">Original encoded image, including orientation and color metadata.</param>
    /// <returns>Owned pixels and measurements, an expected review outcome, or a failure.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        ReadOnlySpan<byte> encodedImage
    ) => Prepare(encodedImage, Maybe<ReviewedPortraitGeometry>.None, Maybe<int>.None);

    /// <summary>Decodes, detects, and prepares a portrait using reviewed measurements.</summary>
    /// <param name="encodedImage">Original encoded image, including orientation and color metadata.</param>
    /// <param name="geometry">Reviewed measurements bound to this exact source.</param>
    /// <returns>Owned pixels and measurements, an expected review outcome, or a failure.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        ReadOnlySpan<byte> encodedImage,
        ReviewedPortraitGeometry geometry
    ) => Prepare(encodedImage, Maybe.From(geometry), Maybe<int>.None);

    /// <summary>Decodes, detects, and prepares a portrait of an explicitly selected face.</summary>
    /// <param name="encodedImage">Original encoded image, including orientation and color metadata.</param>
    /// <param name="faceIndex">Zero-based face selection in stable display order.</param>
    /// <returns>Owned pixels and measurements, an expected review outcome, or a failure.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        ReadOnlySpan<byte> encodedImage,
        int faceIndex
    ) => Prepare(encodedImage, Maybe<ReviewedPortraitGeometry>.None, Maybe.From(faceIndex));

    /// <summary>Decodes, detects, and prepares a 480 by 640 sRGB portrait.</summary>
    /// <param name="encodedImage">Original encoded image, including orientation and color metadata.</param>
    /// <param name="geometry">Reviewed measurements bound to this exact source, when supplied.</param>
    /// <param name="faceIndex">Explicit zero-based selection, or none for a single face.</param>
    /// <returns>
    /// Owned pixels and measurements, or an expected review outcome. A failure reports malformed
    /// input, mismatched geometry, a disposed service, or a missing, corrupt, or failing model.
    /// </returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        ReadOnlySpan<byte> encodedImage,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> faceIndex
    )
    {
        lock (gate)
        {
            if (disposed)
                return FacialErrors.Disposed().Fail<PortraitPreparationResult>();
            return PortraitImage
                .Decode(encodedImage)
                .Bind(image => PrepareImage(image, geometry, faceIndex));
        }
    }

    /// <summary>Detects and prepares a portrait from owned camera or decoded image samples.</summary>
    /// <param name="image">The exact owned source image.</param>
    /// <returns>Owned pixels and measurements, an expected review outcome, or a failure.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(PortraitImage image) =>
        Prepare(image, Maybe<ReviewedPortraitGeometry>.None, Maybe<int>.None);

    /// <summary>Prepares an owned image with explicit review and face selection.</summary>
    /// <param name="image">The exact owned source image.</param>
    /// <param name="geometry">Reviewed measurements bound to this image identity.</param>
    /// <param name="faceIndex">Explicit face selection when more than one face is detected.</param>
    /// <returns>Owned pixels and measurements, an expected review outcome, or a failure.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        PortraitImage image,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> faceIndex
    )
    {
        lock (gate)
        {
            return disposed
                ? FacialErrors.Disposed().Fail<PortraitPreparationResult>()
                : PrepareImage(image, geometry, faceIndex);
        }
    }

    /// <summary>Analyzes owned source pixels once for preview and subsequent crop review.</summary>
    /// <param name="image">Exact orientation-corrected source pixels.</param>
    /// <returns>Owned detections or a typed model failure.</returns>
    public Result<PortraitAnalysis, FacialError> Analyze(PortraitImage image)
    {
        lock (gate)
            return disposed
                ? FacialErrors.Disposed().Fail<PortraitAnalysis>()
                : detector
                    .Value.Bind(model => model.Detect(image.Pixels))
                    .Bind(faces => PortraitPoseAnalysis.Analyze(image, faces));
    }

    /// <summary>Prepares a cached analysis without repeating face detection.</summary>
    /// <param name="analysis">Source-bound immutable detections.</param>
    /// <returns>The prepared portrait or expected review outcome.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(PortraitAnalysis analysis) =>
        Prepare(analysis, Maybe<ReviewedPortraitGeometry>.None, Maybe<int>.None);

    /// <summary>Prepares reviewed geometry using cached detections.</summary>
    /// <param name="analysis">Source-bound immutable detections.</param>
    /// <param name="geometry">Reviewed points bound to the same source.</param>
    /// <returns>The prepared portrait or a typed review mismatch.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        PortraitAnalysis analysis,
        ReviewedPortraitGeometry geometry
    ) => Prepare(analysis, Maybe.From(geometry), Maybe<int>.None);

    /// <summary>Replans a selected face and renders one source-supported portrait.</summary>
    /// <param name="analysis">Source-bound immutable detections.</param>
    /// <param name="geometry">Optional reviewed source coordinates.</param>
    /// <param name="faceIndex">Explicit selection when several faces were detected.</param>
    /// <returns>Owned prepared pixels, expected review outcome, or a typed failure.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        PortraitAnalysis analysis,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> faceIndex
    ) => Prepare(analysis, geometry, faceIndex, PortraitMouthPolicy.Default);

    /// <summary>Prepares cached detections using an explicit captured-mouth tolerance.</summary>
    /// <param name="analysis">Source-bound immutable detections.</param>
    /// <param name="geometry">Optional reviewed source coordinates.</param>
    /// <param name="faceIndex">Explicit selection when several faces were detected.</param>
    /// <param name="mouthPolicy">Immutable application policy snapshotted for this preparation.</param>
    /// <returns>Owned prepared pixels, named review findings, or a typed failure.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        PortraitAnalysis analysis,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> faceIndex,
        PortraitMouthPolicy mouthPolicy
    )
    {
        lock (gate)
            return disposed
                ? FacialErrors.Disposed().Fail<PortraitPreparationResult>()
                : PortraitPreparation.Prepare(analysis, geometry, faceIndex, mouthPolicy, logger);
    }

    /// <summary>Renders a frozen portrait with explicit source-bound background editing.</summary>
    /// <param name="analysis">Cached detections and optional frozen-source person confidence.</param>
    /// <param name="geometry">Optional imported source-bound geometry.</param>
    /// <param name="faceIndex">Explicit selected face when required.</param>
    /// <param name="mouthPolicy">Fixed captured-mouth tolerance.</param>
    /// <param name="backgroundPolicy">Background editing and person-protection parameters.</param>
    /// <returns>Owned composite pixels and evidence, retaining all acquisition findings.</returns>
    public Result<PortraitPreparationResult, FacialError> Prepare(
        PortraitAnalysis analysis,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> faceIndex,
        PortraitMouthPolicy mouthPolicy,
        PortraitBackgroundPolicy backgroundPolicy
    ) =>
        Prepare(analysis, geometry, faceIndex, mouthPolicy)
            .Bind(portrait => PortraitBackground.Apply(analysis, portrait, backgroundPolicy));

    private Result<PortraitPreparationResult, FacialError> PrepareImage(
        PortraitImage image,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> faceIndex
    ) => Analyze(image).Bind(analysis => Prepare(analysis, geometry, faceIndex));

    /// <summary>Releases native ONNX sessions after any running call completes.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            background.Dispose();
            if (detector.IsValueCreated && detector.Value.IsSuccess)
                detector.Value.Value.Dispose();
        }
    }
}
