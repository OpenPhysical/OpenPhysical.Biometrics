// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class FingerprintCanvas : Control
{
    internal static readonly StyledProperty<Maybe<FingerprintScene>> SceneProperty =
        AvaloniaProperty.Register<FingerprintCanvas, Maybe<FingerprintScene>>(nameof(Scene));
    internal static readonly StyledProperty<bool> ShowOverlayProperty = AvaloniaProperty.Register<
        FingerprintCanvas,
        bool
    >(nameof(ShowOverlay), true);
    internal static readonly StyledProperty<bool> ShowSegmentationProperty =
        AvaloniaProperty.Register<FingerprintCanvas, bool>(nameof(ShowSegmentation), true);
    internal static readonly StyledProperty<bool> InvertProperty = AvaloniaProperty.Register<
        FingerprintCanvas,
        bool
    >(nameof(Invert));
    internal static readonly DirectProperty<FingerprintCanvas, IBrush> PlaceholderBrushProperty =
        AvaloniaProperty.RegisterDirect<FingerprintCanvas, IBrush>(
            nameof(PlaceholderBrush),
            canvas => canvas.PlaceholderBrush
        );
    internal FingerprintViewport Viewport { get; } = new();
    private readonly DisplayBitmapCache display = new();
    private Maybe<Point> drag;

    public FingerprintCanvas()
    {
        Focusable = true;
        ZoomInCommand = new RelayCommand(() => Zoom(1.15));
        ZoomOutCommand = new RelayCommand(() => Zoom(1 / 1.15));
        FitCommand = new RelayCommand(() => Fit());
        ZoomActions =
        [
            new("−", "Zoom out", ZoomOutCommand),
            new("+", "Zoom in", ZoomInCommand),
            new("Fit", "Fit image", FitCommand),
        ];
    }

    public ImmutableArray<CanvasZoomAction> ZoomActions { get; }
    public IRelayCommand ZoomInCommand { get; }
    public IRelayCommand ZoomOutCommand { get; }
    public IRelayCommand FitCommand { get; }

    static FingerprintCanvas() =>
        AffectsRender<FingerprintCanvas>(
            SceneProperty,
            ShowOverlayProperty,
            ShowSegmentationProperty,
            InvertProperty,
            IsFocusedProperty
        );

    public Maybe<FingerprintScene> Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }
    public bool ShowOverlay
    {
        get => GetValue(ShowOverlayProperty);
        set => SetValue(ShowOverlayProperty, value);
    }
    public bool ShowSegmentation
    {
        get => GetValue(ShowSegmentationProperty);
        set => SetValue(ShowSegmentationProperty, value);
    }
    public bool Invert
    {
        get => GetValue(InvertProperty);
        set => SetValue(InvertProperty, value);
    }
    public IBrush ViewportBrush => Invert ? Brushes.Black : Brushes.White;
    public IBrush PlaceholderBrush => Invert ? Brushes.White : Brushes.Black;

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(ViewportBrush, new Rect(Bounds.Size));
        if (Scene.HasValue)
            DrawScene(context, Scene.Value);
        if (IsFocused)
            context.DrawLine(
                new Pen(Brushes.DodgerBlue, 2),
                new Point(4, Bounds.Height - 2),
                new Point(Bounds.Width - 4, Bounds.Height - 2)
            );
    }

    private UnitResult<DesktopError> DrawScene(DrawingContext context, FingerprintScene scene)
    {
        var (scale, offset) = Viewport.Transform(Bounds.Size, scene);
        using var clip = context.PushClip(new Rect(Bounds.Size));
        using var transform = context.PushTransform(
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)
        );
        using var imageClip = context.PushClip(new Rect(new Size(scene.Width, scene.Height)));
        context.FillRectangle(ViewportBrush, new Rect(new Size(scene.Width, scene.Height)));
        FingerprintSceneDrawing.DrawImage(context, scene, display, Invert);
        FingerprintSceneDrawing.Draw(context, scene, ShowSegmentation, ShowOverlay, Invert, scale);
        return UnitResult.Success<DesktopError>();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            display.Dispose();
            Viewport.Show(Scene);
            if (Scene.HasNoValue)
                drag = Maybe<Point>.None;
        }
        if (change.Property == InvertProperty)
            RaisePropertyChanged(
                PlaceholderBrushProperty,
                Invert ? Brushes.Black : Brushes.White,
                PlaceholderBrush
            );
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        display.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        Viewport.ZoomBy(Math.Pow(1.15, e.Delta.Y));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus();
        drag = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (drag.HasNoValue)
            return;
        var current = e.GetPosition(this);
        Viewport.PanBy(current - drag.Value);
        drag = current;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        drag = Maybe<Point>.None;
        e.Pointer.Capture(null!);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!Viewport.HandleKey(e.Key))
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        InvalidateVisual();
    }

    private UnitResult<DesktopError> Zoom(double factor)
    {
        var result = Viewport.ZoomBy(factor);
        InvalidateVisual();
        return result;
    }

    private UnitResult<DesktopError> Fit()
    {
        var result = Viewport.Fit();
        InvalidateVisual();
        return result;
    }
}
