// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Media.Imaging;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class DisplayBitmapCache : IDisposable
{
    private Maybe<WriteableBitmap> source;
    private Maybe<WriteableBitmap> inverted;

    internal Result<Maybe<WriteableBitmap>, DesktopError> Select(
        FingerprintScene scene,
        bool invert
    )
    {
        if (!source.Equals(scene.Image))
        {
            Dispose();
            source = scene.Image;
        }
        if (!invert || source.HasNoValue)
            return scene.Image;
        if (inverted.HasValue)
            return inverted;
        var size = source.Value.PixelSize;
        return DesktopBitmap
            .Create(scene.GrayPixels, size.Width, size.Height, invert: true)
            .Map(bitmap =>
            {
                inverted = Maybe.From(bitmap);
                return inverted;
            });
    }

    public void Dispose()
    {
        inverted.Tap(bitmap => bitmap.Dispose());
        inverted = Maybe<WriteableBitmap>.None;
        source = Maybe<WriteableBitmap>.None;
    }
}
