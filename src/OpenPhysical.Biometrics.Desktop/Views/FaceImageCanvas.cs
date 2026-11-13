// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

/// <summary>Fits unmodified camera pixels and source-bound image guides in one image surface.</summary>
public sealed class FaceImageCanvas : Control
{
    /// <summary>Identifies the owned display image.</summary>
    public static readonly StyledProperty<Maybe<FacePreviewScene>> SceneProperty =
        AvaloniaProperty.Register<FaceImageCanvas, Maybe<FacePreviewScene>>(nameof(Scene));

    /// <summary>Identifies display-only horizontal preview mirroring.</summary>
    public static readonly StyledProperty<bool> MirrorProperty = AvaloniaProperty.Register<
        FaceImageCanvas,
        bool
    >(nameof(Mirror));

    /// <summary>Gets or sets the image scene.</summary>
    public Maybe<FacePreviewScene> Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets or sets preview mirroring without changing saved pixels.</summary>
    public bool Mirror
    {
        get => GetValue(MirrorProperty);
        set => SetValue(MirrorProperty, value);
    }

    /// <summary>Identifies the optional image-framing inspection annotations.</summary>
    public static readonly StyledProperty<Maybe<FaceImageGuides>> GuidesProperty =
        AvaloniaProperty.Register<FaceImageCanvas, Maybe<FaceImageGuides>>(nameof(Guides));

    /// <summary>Identifies optional Google face-detection evidence.</summary>
    public static readonly StyledProperty<Maybe<FaceImageGuides>> DetectionGuidesProperty =
        AvaloniaProperty.Register<FaceImageCanvas, Maybe<FaceImageGuides>>(nameof(DetectionGuides));

    /// <summary>Gets or sets framing annotations without changing source pixels.</summary>
    public Maybe<FaceImageGuides> Guides
    {
        get => GetValue(GuidesProperty);
        set => SetValue(GuidesProperty, value);
    }

    /// <summary>Gets or sets cached detector annotations.</summary>
    public Maybe<FaceImageGuides> DetectionGuides
    {
        get => GetValue(DetectionGuidesProperty);
        set => SetValue(DetectionGuidesProperty, value);
    }

    static FaceImageCanvas() =>
        AffectsRender<FaceImageCanvas>(
            SceneProperty,
            MirrorProperty,
            GuidesProperty,
            DetectionGuidesProperty
        );

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        if (Scene.HasNoValue)
            return;
        var scene = Scene.Value;
        var (scale, offset) = ImageViewportTransform.Fit(
            Bounds.Size,
            new Rect(0, 0, scene.Width, scene.Height),
            1,
            default,
            12
        );
        if (scale <= 0)
            return;
        var transform = Mirror
            ? new Matrix(-scale, 0, 0, scale, offset.X + scene.Width * scale, offset.Y)
            : new Matrix(scale, 0, 0, scale, offset.X, offset.Y);
        using (context.PushTransform(transform))
            FaceCanvasImageDrawing.Draw(context, scene, scale);
        FaceGuideDrawing.Draw(context, Guides, transform, Bounds.Size);
        FaceGuideDrawing.Draw(context, DetectionGuides, transform, Bounds.Size);
    }
}
