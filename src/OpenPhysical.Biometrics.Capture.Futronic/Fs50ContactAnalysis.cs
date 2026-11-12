// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Separates connected finger contact from ridge evidence without changing source pixels.</summary>
public static class Fs50ContactAnalysis
{
    /// <summary>Current versioned contact-evidence algorithm.</summary>
    public const string Version = Fs50EvidencePolicy.Version;

    /// <summary>Analyzes original corrected pixels using validated default settings.</summary>
    /// <param name="pixels">Complete 800 by 750 Gray8 acquisition.</param>
    /// <returns>Frame-associated masks and diagnostics or a precise failure.</returns>
    public static Result<Fs50ContactRegion, CaptureError> Analyze(ReadOnlySpan<byte> pixels) =>
        Analyze(pixels, Fs50EvidencePolicy.Default);

    /// <summary>Analyzes an owned corrected frame without rehashing its immutable source.</summary>
    /// <param name="frame">Complete corrected FS50 acquisition.</param>
    /// <returns>Source-associated original evidence or an invalid-frame failure.</returns>
    public static Result<Fs50ContactRegion, CaptureError> Analyze(CaptureFrame frame) =>
        Analyze(frame, Fs50EvidencePolicy.Default);

    /// <summary>Analyzes an owned frame with explicit original contact choices.</summary>
    /// <param name="frame">Complete corrected FS50 acquisition.</param>
    /// <param name="policy">Validated contact evidence choices.</param>
    /// <returns>Source-associated original evidence or a geometry or argument failure.</returns>
    public static Result<Fs50ContactRegion, CaptureError> Analyze(
        CaptureFrame frame,
        Fs50EvidencePolicy policy
    )
    {
        if (frame is null || policy is null)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "An owned frame and validated contact policy are required."
            );
        if (
            frame.Width != 800
            || frame.Height != 750
            || frame.Stage != CaptureFrame.FrameStage.Corrected
        )
            return new CaptureError(
                CaptureErrorCodes.UnsupportedGeometry,
                "Contact analysis requires a complete corrected FS50 acquisition."
            );
        return AnalyzeCore(frame.Pixels.AsSpan(), policy, frame.PixelDigest);
    }

    /// <summary>Analyzes original pixels without enhancing, cropping or changing geometry.</summary>
    /// <param name="pixels">Complete 800 by 750 Gray8 acquisition.</param>
    /// <param name="policy">Snapshotted validated evidence choices.</param>
    /// <returns>Frame-associated masks and diagnostics or a precise failure.</returns>
    public static Result<Fs50ContactRegion, CaptureError> Analyze(
        ReadOnlySpan<byte> pixels,
        Fs50EvidencePolicy policy
    ) => AnalyzeCore(pixels, policy, Maybe<string>.None);

    private static Result<Fs50ContactRegion, CaptureError> AnalyzeCore(
        ReadOnlySpan<byte> pixels,
        Fs50EvidencePolicy policy,
        Maybe<string> sourceDigest
    )
    {
        if (pixels.Length != 800 * 750)
            return new CaptureError(
                CaptureErrorCodes.UnsupportedGeometry,
                "Contact analysis requires a complete corrected FS50 image."
            );
        if (policy is null)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Contact analysis requires validated evidence settings."
            );
        using var builder = new Fs50EvidenceBuilder(policy);
        using var strong = new Fs50TemporaryPlane();
        using var weak = new Fs50TemporaryPlane();
        _ = Fs50ContactMask.WriteTexture(pixels, policy.Options, strong.Buffer, weak.Buffer);
        _ = builder.Visit(pixels, weak.Buffer, strong.Buffer);
        return new Fs50ContactRegion(builder.Build(pixels, sourceDigest));
    }
}
