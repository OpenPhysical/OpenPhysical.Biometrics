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
    private bool disposed;

    /// <summary>Creates a portrait service whose model sessions initialize on the first call.</summary>
    /// <param name="logger">Status-only logger.</param>
    public PortraitPreparer(ILogger<PortraitPreparer> logger) => this.logger = logger;

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
            var hash = Sha256.HexDigest(encodedImage);
            var decoded = PortraitDecoder.Decode(encodedImage);
            if (decoded.IsFailure)
                return decoded.Error.Fail<PortraitPreparationResult>();
            var source = new PortraitSource(hash, decoded.Value);
            return ReviewedGeometryCheck
                .Resolve(geometry, source, faceIndex)
                .Bind(index =>
                    detector
                        .Value.Bind(model => model.Detect(source.Image))
                        .Bind(faces => Complete(source, geometry, index, faces))
                );
        }
    }

    /// <summary>Releases native ONNX sessions after any running call completes.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            if (detector.IsValueCreated && detector.Value.IsSuccess)
                detector.Value.Value.Dispose();
        }
    }

    private Result<PortraitPreparationResult, FacialError> Complete(
        PortraitSource source,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> index,
        ImmutableArray<PortraitFace> faces
    )
    {
        var review = PortraitFaceSelector.NeedsReview(faces, index);
        return review.HasValue
            ? PortraitOutcomes.Review(source, faces, review.Value).Succeed()
            : PortraitFaceSelector
                .Select(faces, index.GetValueOrDefault(0))
                .Bind(face => Crop(source, geometry, faces, face));
    }

    private Result<PortraitPreparationResult, FacialError> Crop(
        PortraitSource source,
        Maybe<ReviewedPortraitGeometry> geometry,
        ImmutableArray<PortraitFace> faces,
        PortraitFace face
    ) =>
        PortraitFeatures
            .FromMediaPipe(face.Landmarks)
            .Bind(features =>
            {
                var image = source.Image;
                var crop = PortraitCrop.Calculate(
                    face,
                    features,
                    geometry,
                    image.Width,
                    image.Height
                );
                return crop.IsFailure
                    ? PortraitOutcomes
                        .Review(source, faces, new(PortraitStatus.ReviewRequired, crop.Error))
                        .Succeed()
                    : Warp(source, faces, crop.Value);
            });

    private Result<PortraitPreparationResult, FacialError> Warp(
        PortraitSource source,
        ImmutableArray<PortraitFace> faces,
        PortraitCropPlan plan
    ) =>
        source
            .Image.Warp(480, 640, plan.Transform)
            .Map(pixels =>
            {
                var prepared = PortraitOutcomes.Prepared(source, faces, plan, pixels);
                logger.LogDebug(
                    "Portrait preparation completed with {FindingCount} findings.",
                    prepared.Findings.Length
                );
                return prepared;
            });
}
