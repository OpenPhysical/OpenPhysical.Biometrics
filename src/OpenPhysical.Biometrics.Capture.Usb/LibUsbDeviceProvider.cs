// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>USB provider using an explicitly configured native library path.</summary>
/// <param name="libraryPath">Approved absolute libusb library path.</param>
public sealed class LibUsbDeviceProvider(string libraryPath) : IUsbDeviceProvider
{
    /// <summary>Locates the native runtime asset distributed by the pinned NuGet dependency.</summary>
    /// <returns>An absolute published or development-output path, or a missing-runtime failure.</returns>
    public static Result<string, CaptureError> LocateBundledLibrary()
    {
        var filename =
            OperatingSystem.IsMacOS() ? "libusb-1.0.dylib"
            : OperatingSystem.IsWindows() ? "libusb-1.0.dll"
            : "libusb-1.0.so.0";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, filename),
            Path.Combine(
                AppContext.BaseDirectory,
                "runtimes",
                System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                "native",
                filename
            ),
        };
        foreach (var candidate in candidates)
            if (File.Exists(candidate))
                return candidate;
        return new CaptureError(
            CaptureErrorCodes.DeviceUnavailable,
            "The libusb native runtime asset is missing from the application output."
        );
    }

    /// <inheritdoc/>
    public ValueTask<Result<ImmutableArray<UsbDeviceInfo>, CaptureError>> DiscoverAsync(
        ushort vendorId,
        ushort productId,
        CancellationToken cancellationToken
    ) => LibUsbDeviceTransport.DiscoverAsync(libraryPath, vendorId, productId, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<Result<IUsbDeviceTransport, CaptureError>> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    )
    {
        var result = await LibUsbDeviceTransport
            .OpenAsync(libraryPath, device, cancellationToken)
            .ConfigureAwait(false);
        return result.Map(transport => (IUsbDeviceTransport)transport);
    }
}
