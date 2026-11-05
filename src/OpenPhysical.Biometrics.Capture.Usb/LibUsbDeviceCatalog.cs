// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

internal static class LibUsbDeviceCatalog
{
    internal static Result<T, CaptureError> WithDevices<T>(
        LibUsbNative api,
        Func<ImmutableArray<(IntPtr Handle, UsbDeviceInfo Info)>, Result<T, CaptureError>> use
    )
    {
        var count = api.GetDeviceList(api.Context, out var list);
        if (count < 0)
        {
            return Result.Failure<T, CaptureError>(
                new CaptureError(CaptureErrorCodes.DeviceUnavailable, "USB enumeration failed.")
            );
        }

        try
        {
            if (count > 65536 || (count > 0 && list == IntPtr.Zero))
            {
                return Result.Failure<T, CaptureError>(
                    new CaptureError(
                        CaptureErrorCodes.DeviceUnavailable,
                        "Invalid USB enumeration."
                    )
                );
            }

            var devices = ReadDevices(api, list, (int)count);
            return devices.IsFailure
                ? Result.Failure<T, CaptureError>(devices.Error)
                : use(devices.Value);
        }
        finally
        {
            if (list != IntPtr.Zero)
            {
                api.FreeDeviceList(list, 1);
            }
        }
    }

    private static Result<
        ImmutableArray<(IntPtr Handle, UsbDeviceInfo Info)>,
        CaptureError
    > ReadDevices(LibUsbNative api, IntPtr list, int count)
    {
        var devices = ImmutableArray.CreateBuilder<(IntPtr Handle, UsbDeviceInfo Info)>();
        for (var index = 0; index < count; index++)
        {
            var device = Marshal.ReadIntPtr(list, index * IntPtr.Size);
            var descriptor = new byte[18];
            if (device == IntPtr.Zero || api.GetDescriptor(device, descriptor) != 0)
            {
                return Result.Failure<
                    ImmutableArray<(IntPtr Handle, UsbDeviceInfo Info)>,
                    CaptureError
                >(
                    new CaptureError(
                        CaptureErrorCodes.DeviceUnavailable,
                        "USB descriptor read failed."
                    )
                );
            }

            devices.Add(
                (
                    device,
                    new UsbDeviceInfo(
                        BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(8, 2)),
                        BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(10, 2)),
                        api.GetBus(device),
                        api.GetAddress(device)
                    )
                )
            );
        }

        return devices.ToImmutable();
    }
}
