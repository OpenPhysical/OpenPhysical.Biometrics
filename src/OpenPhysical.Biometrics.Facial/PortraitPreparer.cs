// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Prepares source-supported PIV Full Frontal portraits using MediaPipe geometry.</summary>
/// <remarks>Calls are serialized with disposal. Returned buffers are immutable and owned by the result.</remarks>
public sealed class PortraitPreparer : IDisposable
{
    private readonly object gate = new();
    private readonly Lazy<MediaPipeDetector> detector = new(() => new());
    private readonly ILogger<PortraitPreparer> logger;
    private bool disposed;

    /// <summary>Creates a portrait service whose model sessions initialize on the first call.</summary>
    /// <param name="logger">Optional status-only logger.</param>
    public PortraitPreparer(ILogger<PortraitPreparer>? logger = null) =>
        this.logger = logger ?? NullLogger<PortraitPreparer>.Instance;

    /// <summary>Decodes, detects, and prepares a 480 by 640 sRGB portrait.</summary>
    /// <param name="encodedImage">Original encoded image, including orientation and color metadata.</param>
    /// <param name="geometry">Reviewed measurements bound to this exact source, or null.</param>
    /// <param name="faceIndex">Explicit zero-based selection, or null for a single face.</param>
    /// <returns>Owned pixels and measurements, or an expected review outcome.</returns>
    /// <exception cref="ArgumentException">Input or reviewed geometry is malformed or mismatched.</exception>
    /// <exception cref="ObjectDisposedException">The service has been disposed.</exception>
    /// <exception cref="InvalidOperationException">A packaged model is missing, corrupt, or returns invalid data.</exception>
    public PortraitPreparationResult Prepare(
        ReadOnlySpan<byte> encodedImage,
        ReviewedPortraitGeometry? geometry = null,
        int? faceIndex = null
    )
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var hash = Sha256.HexDigest(encodedImage);
            var image = PortraitPixels.Decode(encodedImage);
            if (geometry is not null)
            {
                if (
                    geometry.Version != 1
                    || !hash.Equals(geometry.SourceSha256, StringComparison.OrdinalIgnoreCase)
                    || geometry.Width != image.Width
                    || geometry.Height != image.Height
                    || geometry.FaceIndex < 0
                    || (faceIndex.HasValue && faceIndex.Value != geometry.FaceIndex)
                )
                    throw new ArgumentException(
                        "Reviewed geometry requires the matching source and face selection.",
                        nameof(geometry)
                    );
                foreach (
                    var point in new[]
                    {
                        geometry.Crown,
                        geometry.Chin,
                        geometry.ViewerLeftEar,
                        geometry.ViewerRightEar,
                    }.OfType<PortraitPoint>()
                )
                    if (
                        !double.IsFinite(point.X)
                        || !double.IsFinite(point.Y)
                        || point.X < 0
                        || point.Y < 0
                        || point.X >= image.Width
                        || point.Y >= image.Height
                    )
                        throw new ArgumentException(
                            "Reviewed points must lie within the oriented source.",
                            nameof(geometry)
                        );
                faceIndex = geometry.FaceIndex;
            }
            var faces = detector.Value.Detect(image);
            PortraitPreparationResult Failure(PortraitStatus status, string code, string message) =>
                new(
                    status,
                    hash,
                    image.Width,
                    image.Height,
                    faces,
                    null,
                    null,
                    [],
                    [new(code, message)]
                );
            if (faces.IsEmpty)
                return Failure(
                    PortraitStatus.NoFace,
                    "NoFace",
                    "No face passed detection and presence checks."
                );
            if (!faceIndex.HasValue && faces.Length != 1)
                return Failure(
                    PortraitStatus.FaceSelectionRequired,
                    "SelectFace",
                    "Select one face by its stable index."
                );
            var index = faceIndex ?? 0;
            if (index < 0 || index >= faces.Length)
                throw new ArgumentOutOfRangeException(nameof(faceIndex));
            var face = faces[index];
            var crop = PortraitCrop.Calculate(face, geometry, image.Width, image.Height);
            if (crop.Transform is null)
                return Failure(PortraitStatus.ReviewRequired, crop.Code, crop.Message);
            var pixels = image.Warp(480, 640, crop.Transform);
            var findings = crop.Measurements!.ReviewedHeadBoundary
                ? ImmutableArray<PortraitFinding>.Empty
                :
                [
                    new PortraitFinding(
                        "EstimatedHeadBoundary",
                        "Crown and ear geometry includes automatic estimates; review against the original capture."
                    ),
                ];
            logger.LogDebug(
                "Portrait preparation completed with {FindingCount} findings.",
                findings.Length
            );
            return new(
                PortraitStatus.Prepared,
                hash,
                image.Width,
                image.Height,
                faces,
                crop.Transform,
                crop.Measurements,
                ImmutableArray.Create(pixels),
                findings
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
            if (detector.IsValueCreated)
                detector.Value.Dispose();
        }
    }
}

internal static class PortraitCrop
{
    internal static (
        PortraitTransform? Transform,
        PortraitMeasurements? Measurements,
        string Code,
        string Message
    ) Calculate(PortraitFace face, ReviewedPortraitGeometry? review, int width, int height)
    {
        var features = face.Features;
        static PortraitPoint Mid(PortraitPoint a, PortraitPoint b) =>
            new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var leftEye = features.ViewerLeftEye;
        var rightEye = features.ViewerRightEye;
        var eyes = Mid(leftEye, rightEye);
        var angle = Math.Atan2(rightEye.Y - leftEye.Y, rightEye.X - leftEye.X);
        var rotation = new PortraitTransform(Math.Cos(angle), Math.Sin(angle), 0, 0);
        var chin = rotation.Map(review?.Chin ?? features.Chin);
        var eye = rotation.Map(eyes);
        var nose = rotation.Map(features.NoseBridge);
        var mouth = rotation.Map(features.MouthCenter);
        var left = rotation.Map(review?.ViewerLeftEar ?? face.ViewerLeftEar);
        var right = rotation.Map(review?.ViewerRightEar ?? face.ViewerRightEar);
        // Estimate the crown from the eye-to-chin distance and retain its estimated origin.
        var crown = review?.Crown is { } supplied
            ? rotation.Map(supplied)
            : new PortraitPoint(eye.X, eye.Y - (chin.Y - eye.Y) * .9);
        var headWidth = right.X - left.X;
        var headHeight = chin.Y - crown.Y;
        if (headWidth < 240 || headHeight <= 0 || crown.Y >= eye.Y || eye.Y >= chin.Y)
            return (
                null,
                null,
                "HeadGeometry",
                "Review head boundaries or supply a higher-resolution capture."
            );
        // A uniform transform preserves proportions. Bound scale before solving source coverage.
        var minScale = 240 / headWidth;
        var maxScale = Math.Min(1, Math.Min(480 * 4.0 / (7 * headWidth), 512 / headHeight));
        if (minScale > maxScale)
            return (
                null,
                null,
                "HeadProportions",
                "Head proportions require review to fit the portrait profile."
            );
        var center = (nose.X + mouth.X) / 2;
        // Feasible scale and eye-height pairs form a convex polygon. Clipping the
        // polygon avoids missing a narrow feasible crop between sampled scales.
        var feasible = new List<(double Scale, double Eye)>
        {
            (minScale, 192),
            (maxScale, 192),
            (maxScale, 320),
            (minScale, 320),
        };
        void Clip(double a, double b, double limit)
        {
            var output = new List<(double Scale, double Eye)>();
            for (var i = 0; i < feasible.Count; i++)
            {
                var start = feasible[i];
                var end = feasible[(i + 1) % feasible.Count];
                var first = a * start.Scale + b * start.Eye - limit;
                var last = a * end.Scale + b * end.Eye - limit;
                if (first <= 0)
                    output.Add(start);
                if ((first < 0 && last > 0) || (first > 0 && last < 0))
                {
                    var fraction = first / (first - last);
                    output.Add(
                        (
                            start.Scale + fraction * (end.Scale - start.Scale),
                            start.Eye + fraction * (end.Eye - start.Eye)
                        )
                    );
                }
            }
            feasible = output;
        }
        // Approximate horizontal centering uses the documented two-percent tolerance.
        Clip(Math.Abs(nose.X - mouth.X), 0, 19.2);
        Clip(eye.Y - crown.Y, -1, 0);
        Clip(chin.Y - eye.Y, 1, 639);
        Clip(center - left.X, 0, 240);
        Clip(right.X - center, 0, 239);
        var sourceX = rotation.A * center - rotation.B * eye.Y;
        var sourceY = rotation.B * center + rotation.A * eye.Y;
        foreach (
            var corner in new[] { new PortraitPoint(0, 0), new(479, 0), new(0, 639), new(479, 639) }
        )
        {
            var x = rotation.A * (corner.X - 240) - rotation.B * corner.Y;
            var y = rotation.B * (corner.X - 240) + rotation.A * corner.Y;
            Clip(-sourceX, -rotation.B, x);
            Clip(sourceX - width + 1, rotation.B, -x);
            Clip(-sourceY, rotation.A, y);
            Clip(sourceY - height + 1, -rotation.A, -y);
        }
        if (feasible.Count == 0)
            return (
                null,
                null,
                "SourceCoverage",
                "A centered full-head crop requires additional source coverage or resolution."
            );

        // Express both axes in the policy's normalized target units. The nearest
        // point is either the target itself or its projection onto a polygon edge.
        var normalized = feasible
            .Select(p => (X: (p.Scale * headWidth - 256) / 16, Y: (p.Eye - 256) / 64))
            .ToArray();
        (double X, double Y) best = normalized[0];
        var bestCost = double.PositiveInfinity;
        var twiceArea = normalized
            .Select(
                (p, i) =>
                    p.X * normalized[(i + 1) % normalized.Length].Y
                    - p.Y * normalized[(i + 1) % normalized.Length].X
            )
            .Sum();
        var containsTarget = twiceArea > 1e-12;
        for (var i = 0; i < normalized.Length; i++)
        {
            var start = normalized[i];
            var end = normalized[(i + 1) % normalized.Length];
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            if (dx * -start.Y - dy * -start.X < -1e-10)
                containsTarget = false;
            var length = dx * dx + dy * dy;
            var fraction =
                length > 0 ? Math.Clamp(-(start.X * dx + start.Y * dy) / length, 0, 1) : 0;
            var candidate = (X: start.X + fraction * dx, Y: start.Y + fraction * dy);
            var cost = candidate.X * candidate.X + candidate.Y * candidate.Y;
            if (cost < bestCost)
            {
                best = candidate;
                bestCost = cost;
            }
        }
        if (containsTarget)
            best = (0, 0);
        var scale = (best.X * 16 + 256) / headWidth;
        var eyeY = best.Y * 64 + 256;
        var transform = new PortraitTransform(
            rotation.A * scale,
            rotation.B * scale,
            240 - center * scale,
            eyeY - eye.Y * scale
        );
        var measures = new PortraitMeasurements(
            scale * headWidth,
            scale * headHeight,
            eyeY / 640,
            review?.Crown is not null
                && review.ViewerLeftEar is not null
                && review.ViewerRightEar is not null
        )
        {
            CrownOrigin = review?.Crown is null
                ? PortraitMeasurementOrigin.Estimated
                : PortraitMeasurementOrigin.Reviewed,
            ViewerLeftEarOrigin = review?.ViewerLeftEar is null
                ? PortraitMeasurementOrigin.Estimated
                : PortraitMeasurementOrigin.Reviewed,
            ViewerRightEarOrigin = review?.ViewerRightEar is null
                ? PortraitMeasurementOrigin.Estimated
                : PortraitMeasurementOrigin.Reviewed,
            ChinOrigin = review?.Chin is null
                ? PortraitMeasurementOrigin.Mesh
                : PortraitMeasurementOrigin.Reviewed,
        };
        return (transform, measures, "", "");
    }
}

/// <summary>Registers optional MediaPipe portrait processing.</summary>
public static class PortraitServiceCollectionExtensions
{
    /// <summary>Adds the lazily initialized portrait preparer.</summary>
    /// <param name="services">Application services.</param>
    /// <returns>The same collection for composition.</returns>
    public static IServiceCollection AddPortraitPreparation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<PortraitPreparer>();
        return services;
    }
}
