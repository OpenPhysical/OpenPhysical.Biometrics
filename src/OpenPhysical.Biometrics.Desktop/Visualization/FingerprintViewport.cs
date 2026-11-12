// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class FingerprintViewport
{
    private Maybe<FingerprintSceneFraming> framing;
    internal double Zoom { get; private set; } = 1;
    internal Vector Pan { get; private set; }

    internal UnitResult<DesktopError> Show(Maybe<FingerprintScene> scene)
    {
        var next = scene.Bind(value => value.Framing);
        if (
            next.HasValue
            && framing.HasValue
            && Equals(next.Value.Source, framing.Value.Source)
            && framing.Value.Final
            && !next.Value.Final
        )
            return UnitResult.Success<DesktopError>();
        var changed =
            next.HasValue != framing.HasValue
            || next.HasValue
                && (
                    !Equals(next.Value.Source, framing.Value.Source)
                    || !framing.Value.Final && next.Value.Final
                );
        framing = next;
        return scene.HasNoValue || changed ? Fit() : UnitResult.Success<DesktopError>();
    }

    internal (double Scale, Vector Offset) Transform(Size viewport, FingerprintScene scene)
    {
        var region = framing.Match(
            value => value.Region,
            () => new Rect(0, 0, scene.Width, scene.Height)
        );
        var padding = framing.Match(value => value.Final ? 12.0 : 0, () => 0);
        padding = Math.Min(padding, Math.Min(viewport.Width, viewport.Height) / 4);
        var scale =
            Math.Min(
                (viewport.Width - padding * 2) / region.Width,
                (viewport.Height - padding * 2) / region.Height
            ) * Zoom;
        var offset =
            new Vector(
                viewport.Width / 2 - region.Center.X * scale,
                viewport.Height / 2 - region.Center.Y * scale
            ) + Pan;
        return (scale, offset);
    }

    internal bool HandleKey(Key key) =>
        key switch
        {
            Key.Add or Key.OemPlus => ZoomBy(1.15).IsSuccess,
            Key.Subtract or Key.OemMinus => ZoomBy(1 / 1.15).IsSuccess,
            Key.Left => PanBy(new Vector(-16, 0)).IsSuccess,
            Key.Right => PanBy(new Vector(16, 0)).IsSuccess,
            Key.Up => PanBy(new Vector(0, -16)).IsSuccess,
            Key.Down => PanBy(new Vector(0, 16)).IsSuccess,
            Key.Home => Fit().IsSuccess,
            _ => false,
        };

    internal UnitResult<DesktopError> ZoomBy(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
            return new DesktopError("Viewport.Zoom", "Zoom requires a positive finite factor.");
        Zoom = Math.Clamp(Zoom * factor, 0.5, 8);
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> PanBy(Vector delta)
    {
        Pan += delta;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Fit()
    {
        Zoom = 1;
        Pan = default;
        return UnitResult.Success<DesktopError>();
    }
}
