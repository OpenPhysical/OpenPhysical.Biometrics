// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal sealed class PreviewImagePreparation
{
    private readonly object sync = new();
    private Maybe<FingerprintProcessingMask> wholeFrame;

    internal Result<FingerprintPreprocessingArtifact, DesktopError> Prepare(
        CaptureFrame frame,
        (FingerprintPreprocessingPolicy Policy, long Revision) settings,
        CancellationToken token
    )
    {
        var policy = FingerprintPreprocessingPolicy.Create(
            false,
            settings.Policy.TileSize,
            settings.Policy.ClipLimit,
            0,
            settings.Policy.Brightness,
            settings.Policy.Contrast
        );
        return policy
            .MapError(error => new DesktopError(error.Code, error.Message))
            .Bind(tone =>
                Mask(frame)
                    .Bind(mask =>
                        FingerprintPreprocessing
                            .Process(frame, mask, tone, settings.Revision, token)
                            .MapError(error => new DesktopError(error.Code, error.Message))
                    )
            );
    }

    private Result<FingerprintProcessingMask, DesktopError> Mask(CaptureFrame frame)
    {
        lock (sync)
        {
            if (
                wholeFrame.HasValue
                && wholeFrame.Value.Width == frame.Width
                && wholeFrame.Value.Height == frame.Height
            )
                return wholeFrame.Value;
            var pixels = new byte[frame.Pixels.Length];
            Array.Fill(pixels, (byte)1);
            var created = FingerprintProcessingMask
                .Create(frame.Width, frame.Height, pixels, pixels)
                .MapError(error => new DesktopError(error.Code, error.Message));
            if (created.IsSuccess)
                wholeFrame = created.Value;
            return created;
        }
    }
}
