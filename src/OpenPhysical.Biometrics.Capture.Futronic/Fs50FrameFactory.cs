// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto.Digests;

internal sealed class Fs50FrameFactory(
    Fs50GeometryMap map,
    ImmutableArray<byte> digest,
    CaptureDeviceDescriptor device
)
{
    internal static ImmutableArray<byte> Digest(
        ImmutableArray<byte> descriptor,
        ImmutableArray<byte> controls,
        ImmutableArray<byte> calibration
    )
    {
        // Fixed lengths make descriptor || controls || calibration unambiguous.
        var hash = new Sha256Digest();
        hash.BlockUpdate(descriptor.AsSpan());
        hash.BlockUpdate(controls.AsSpan());
        hash.BlockUpdate(calibration.AsSpan());
        var bytes = new byte[hash.GetDigestSize()];
        _ = hash.DoFinal(bytes);
        return ImmutableArray.CreateRange(bytes);
    }

    internal Result<CaptureFrame, CaptureError> Create(
        ImmutableArray<byte> raw,
        CaptureSettings settings,
        long sequence,
        DateTimeOffset timestamp
    )
    {
        var pixels = settings.Corrected
            ? Fs50Resampler.Correct(raw.AsSpan(), map)
            : Result.Success<ImmutableArray<byte>, CaptureError>(raw);
        if (pixels.IsFailure)
            return pixels.Error;
        var rawGeometry = CaptureGeometry
            .Create(Fs50Descriptor.RawWidth, Fs50Descriptor.RawHeight)
            .Value;
        var output = settings.Corrected
            ? CaptureGeometry.Create(Fs50Descriptor.Width, Fs50Descriptor.Height).Value
            : rawGeometry;
        return CaptureProvenance
            .Create(
                device,
                "1.0.0",
                rawGeometry,
                output,
                settings,
                settings.Corrected ? digest.AsSpan() : ReadOnlySpan<byte>.Empty,
                settings.Corrected ? Fs50Resampler.ProfileId : string.Empty,
                CaptureResolution.Unknown,
                CaptureQualification.Experimental
            )
            .Bind(provenance =>
                CaptureFrame.Create(pixels.Value.AsSpan(), sequence, timestamp, provenance)
            );
    }
}
