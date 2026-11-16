// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class AvFoundationCatalog
{
    internal static Result<ImmutableArray<CameraSource>, CameraError> Read()
    {
        var status = AvFoundationNative.opbc_list(out var pointer);
        try
        {
            if (status != 0)
                return AvFoundationStatus.Error(status);
            if (pointer == nint.Zero)
                return AvFoundationStatus.Error(5);
            var json = Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
            if (json.Length is < 2 or > 1024 * 1024)
                return AvFoundationStatus.Error(5);
            var sources = JsonSerializer.Deserialize<ImmutableArray<CameraSource>>(json);
            return
                !sources.IsDefault
                && sources.Length <= 64
                && sources.Select(value => value.Id).Distinct().Count() == sources.Length
                && sources.All(Valid)
                ? sources.Where(value => !value.Modes.IsEmpty).ToImmutableArray()
                : AvFoundationStatus.Error(5);
        }
        finally
        {
            if (pointer != nint.Zero)
                AvFoundationNative.opbc_free(pointer);
        }
    }

    private static bool Valid(CameraSource source) =>
        source is not null
        && !string.IsNullOrWhiteSpace(source.Id)
        && !string.IsNullOrWhiteSpace(source.Name)
        && !source.Modes.IsDefault
        && source.Modes.Length <= 512
        && source.Modes.All(mode =>
            mode is not null
            && mode.Width > 0
            && mode.Height > 0
            && (long)mode.Width * mode.Height <= 32 * 1024 * 1024
            && mode.PixelFormat == "BGRA32"
            && mode.FrameRateNumerator > 0
            && mode.FrameRateDenominator > 0
            && mode.FramesPerSecond <= 240
        );
}
