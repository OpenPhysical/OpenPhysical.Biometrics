// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

internal static class SceneFactory
{
    internal static Result<FingerprintScene, DesktopError> Selected(
        FingerCaptureCandidate candidate
    ) => Frame(candidate.Frame, []);

    internal static Result<FingerprintScene, DesktopError> Aligned(
        CapturedFingerprint probe,
        FingerprintAlignmentResult alignment
    ) =>
        Captured(probe, false)
            .Map(scene => scene with { ReferenceOverlay = alignment.ReferenceMinutiae });

    internal static Result<FingerprintScene, DesktopError> Captured(
        CapturedFingerprint capture,
        bool features = true
    ) =>
        capture.Processing.Match(
            evidence =>
                Processed(evidence.Artifact, features ? capture.View.Minutiae : [])
                    .Map(scene =>
                        scene with
                        {
                            Foreground = ForegroundBoundary.Create(
                                evidence.Contour.FeatureMask,
                                evidence.Artifact.Width,
                                evidence.Artifact.Height
                            ),
                            Framing = FingerprintSceneFraming.Context(
                                capture.Frame,
                                evidence.Artifact.Mask.Retained,
                                evidence.Artifact.Width,
                                evidence.Artifact.Height
                            ),
                        }
                    ),
            () =>
                Frame(capture.Frame, features ? capture.View.Minutiae : [], capture.Segmentation)
                    .Map(scene =>
                        scene with
                        {
                            Framing = FingerprintSceneFraming.Context(
                                capture.Frame,
                                [],
                                capture.Frame.Width,
                                capture.Frame.Height
                            ),
                        }
                    )
        );

    internal static Result<FingerprintScene, DesktopError> Processed(
        FingerprintPreprocessingArtifact artifact,
        ImmutableArray<FingerprintMinutia> points
    ) =>
        DesktopBitmap
            .Create(artifact.Pixels, artifact.Width, artifact.Height)
            .Map(bitmap => new FingerprintScene(
                artifact.Width,
                artifact.Height,
                points,
                Maybe.From(bitmap)
            )
            {
                GrayPixels = artifact.Pixels,
            });

    internal static Result<FingerprintScene, DesktopError> FrozenProcessed(
        FingerprintPreprocessingArtifact artifact
    ) =>
        Processed(artifact, [])
            .Map(scene =>
                scene with
                {
                    Framing = FingerprintSceneFraming.Full(
                        artifact.OriginalFrame,
                        artifact.Width,
                        artifact.Height
                    ),
                }
            );

    internal static Result<FingerprintScene, DesktopError> Frame(
        CaptureFrame frame,
        ImmutableArray<FingerprintMinutia> points,
        Maybe<Fs50ContactRegion> segmentation = default
    ) =>
        DesktopBitmap
            .Create(frame.Pixels, frame.Width, frame.Height)
            .Map(bitmap => new FingerprintScene(
                frame.Width,
                frame.Height,
                points,
                Maybe.From(bitmap)
            )
            {
                GrayPixels = frame.Pixels,
                Foreground = segmentation.Match(
                    region => ForegroundBoundary.Create(region, frame.Width, frame.Height),
                    () => ImmutableArray<ForegroundEdge>.Empty
                ),
            });

    internal static Result<FingerprintScene, DesktopError> Document(
        TemplateDocument document,
        int viewIndex
    )
    {
        var record = document.Record;
        var points = record.Views[viewIndex].Minutiae;
        var images = document.Images.Where(image => image.ViewIndex == viewIndex).ToArray();
        var framing = FingerprintSceneFraming.Reference(document, viewIndex, images);
        return images.Length == 0
            ? new FingerprintScene(
                record.Width,
                record.Height,
                points,
                Maybe<Avalonia.Media.Imaging.WriteableBitmap>.None
            )
            {
                Framing = framing,
            }
            : DesktopBitmap
                .Create(images[0].GrayPixels, images[0].Width, images[0].Height)
                .Map(bitmap => new FingerprintScene(
                    images[0].Width,
                    images[0].Height,
                    points,
                    Maybe.From(bitmap)
                )
                {
                    GrayPixels = images[0].GrayPixels,
                    Foreground = Outline(images[0]),
                    Framing = framing,
                });
    }

    private static ImmutableArray<ForegroundEdge> Outline(TemplateImage image)
    {
        if (image.Processing.HasValue)
            return image.Processing.Value.Contour.Match(
                contour =>
                    ForegroundBoundary.Create(contour.FeatureMask, image.Width, image.Height),
                () => ImmutableArray<ForegroundEdge>.Empty
            );
        if (image.Width != 800 || image.Height != 750)
            return [];
        var region = Fs50ContactAnalysis.Analyze(image.GrayPixels.AsSpan());
        return region.IsSuccess
            ? ForegroundBoundary.Create(region.Value, image.Width, image.Height)
            : [];
    }
}
