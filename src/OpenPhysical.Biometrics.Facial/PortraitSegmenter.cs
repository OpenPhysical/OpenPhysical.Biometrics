// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

/// <summary>Estimates person confidence from immutable source pixels using Google's square Selfie Segmenter.</summary>
/// <remarks>Reuse one instance. Calls serialize with disposal; results remain usable after disposal.</remarks>
public sealed class PortraitSegmenter : IDisposable
{
    private readonly object gate = new();
    private readonly Lazy<Result<SelfieSegmentationModel, FacialError>> model = new(
        SelfieSegmentationModel.Create
    );
    private readonly ILogger logger;
    private bool disposed;

    /// <summary>Creates a lazy, reusable segmentation service.</summary>
    /// <param name="logger">Status-only logger; image content and identities are never logged.</param>
    public PortraitSegmenter(ILogger logger) => this.logger = logger;

    /// <summary>Segments one frozen source image without changing its pixels or geometry.</summary>
    /// <param name="image">The exact immutable source image.</param>
    /// <returns>A source-bound confidence mask, or a typed model/runtime failure.</returns>
    public Result<PortraitPersonMask, FacialError> Segment(PortraitImage image)
    {
        lock (gate)
        {
            if (disposed)
                return FacialErrors.Disposed().Fail<PortraitPersonMask>();
            return model
                .Value.Bind(active => active.Segment(image))
                .TapError(_ => logger.LogWarning("Person segmentation could not be completed."));
        }
    }

    /// <summary>Waits for an active call and releases the native session.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            if (model.IsValueCreated && model.Value.IsSuccess)
                model.Value.Value.Dispose();
        }
    }
}
