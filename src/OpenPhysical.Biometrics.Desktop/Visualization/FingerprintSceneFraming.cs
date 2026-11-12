// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using Avalonia;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed record FingerprintSceneFraming(object Source, Rect Region, bool Final)
{
    internal static FingerprintSceneFraming Reference(
        TemplateDocument document,
        int viewIndex,
        TemplateImage[] images
    ) =>
        images.Length == 0
            ? Context((document, viewIndex), [], document.Record.Width, document.Record.Height)
            : Context(
                (document, viewIndex),
                images[0]
                    .Processing.Match(
                        value => value.Mask.Retained,
                        () => ImmutableArray<byte>.Empty
                    ),
                images[0].Width,
                images[0].Height
            );

    internal static FingerprintSceneFraming Full(object source, int width, int height) =>
        new(source, new Rect(0, 0, width, height), false);

    internal static FingerprintSceneFraming Context(
        object source,
        ImmutableArray<byte> mask,
        int width,
        int height
    )
    {
        int left = width,
            top = height,
            right = -1,
            bottom = -1;
        for (var i = 0; i < mask.Length; i++)
        {
            if (mask[i] == 0)
                continue;
            left = Math.Min(left, i % width);
            right = Math.Max(right, i % width);
            top = Math.Min(top, i / width);
            bottom = Math.Max(bottom, i / width);
        }
        var region =
            right < left
                ? new Rect(0, 0, width, height)
                : new Rect(left, top, right - left + 1, bottom - top + 1);
        return new(source, region, true);
    }
}
