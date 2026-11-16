// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Versioned, geometry-preserving preparation outside native NIST algorithms.</summary>
public static class FingerprintPreprocessing
{
    /// <summary>Current deterministic CLAHE, mask and white-anchored tone implementation.</summary>
    public const string Version = "gray8-masked-clahe-white-anchor-v1";

    /// <summary>Prepares an immutable image without assigning an operator revision.</summary>
    /// <param name="original">Unchanged acquisition.</param>
    /// <param name="mask">Retained and inner image regions.</param>
    /// <param name="policy">Snapshotted validated settings.</param>
    /// <param name="cancellationToken">Cancellation prevents artifact publication.</param>
    /// <returns>An immutable association or a precise failure.</returns>
    public static Result<FingerprintPreprocessingArtifact, CaptureError> Process(
        CaptureFrame original,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        CancellationToken cancellationToken
    ) => Process(original, mask, policy, 0, cancellationToken);

    /// <summary>Prepares exact processing bytes bound to source, masks, policy and revision.</summary>
    /// <param name="original">Unchanged acquisition.</param>
    /// <param name="mask">Retained and inner image regions.</param>
    /// <param name="policy">Snapshotted validated settings.</param>
    /// <param name="revision">Nonnegative operator processing revision.</param>
    /// <param name="cancellationToken">Cancellation prevents artifact publication.</param>
    /// <returns>An immutable association or a precise failure.</returns>
    public static Result<FingerprintPreprocessingArtifact, CaptureError> Process(
        CaptureFrame original,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        long revision,
        CancellationToken cancellationToken
    )
    {
        if (
            original is null
            || mask is null
            || policy is null
            || revision < 0
            || original.Width != mask.Width
            || original.Height != mask.Height
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Source, mask geometry, policy or processing revision is invalid."
            );
        if (cancellationToken.IsCancellationRequested)
            return new CaptureError(
                CaptureErrorCodes.Cancelled,
                "Image preparation was cancelled."
            );
        return Prepare(original, mask, policy, revision, cancellationToken);
    }

    private static Result<FingerprintPreprocessingArtifact, CaptureError> Prepare(
        CaptureFrame original,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        long revision,
        CancellationToken cancellationToken
    )
    {
        if (policy.IsOriginal)
            return new FingerprintPreprocessingArtifact(
                original,
                mask,
                policy,
                original.Pixels,
                revision
            );
        var prepared =
            policy.ClaheEnabled && policy.Blend > 0
                ? FingerprintClahe.Apply(original, mask, policy, cancellationToken)
                : Result.Success<byte[], CaptureError>(original.Pixels.ToArray());
        if (prepared.IsFailure)
            return prepared.Error;
        return Finish(original, mask, policy, revision, prepared.Value, cancellationToken);
    }

    private static Result<FingerprintPreprocessingArtifact, CaptureError> Finish(
        CaptureFrame original,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        long revision,
        byte[] prepared,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!ApplyTone(prepared, original.Width, mask, policy, cancellationToken))
                return new CaptureError(
                    CaptureErrorCodes.Cancelled,
                    "Image preparation was cancelled."
                );
            return new FingerprintPreprocessingArtifact(
                original,
                mask,
                policy,
                ImmutableArray.CreateRange(prepared),
                revision
            );
        }
        finally
        {
            Array.Clear(prepared);
        }
    }

    private static bool ApplyTone(
        byte[] pixels,
        int width,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        CancellationToken cancellationToken
    )
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            if (i % width == 0 && cancellationToken.IsCancellationRequested)
                return false;
            double bright = Math.Clamp(pixels[i] + policy.BrightnessOffset, 0, 255);
            pixels[i] =
                mask.Retained[i] == 0
                    ? (byte)255
                    : (byte)
                        Math.Clamp(
                            Math.Round(255 - ((255 - bright) * policy.ContrastGain)),
                            0,
                            255
                        );
        }
        return !cancellationToken.IsCancellationRequested;
    }
}
