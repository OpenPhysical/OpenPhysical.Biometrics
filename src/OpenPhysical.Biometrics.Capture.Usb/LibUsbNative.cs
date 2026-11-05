// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

internal sealed class LibUsbNative : IAsyncDisposable
{
    private readonly IntPtr library;
    private readonly ExitFunction exit;
    private int disposed;

    private LibUsbNative(IntPtr library)
    {
        this.library = library;
        Init = Symbol<InitFunction>("libusb_init");
        exit = Symbol<ExitFunction>("libusb_exit");
        GetDeviceList = Symbol<DeviceListFunction>("libusb_get_device_list");
        FreeDeviceList = Symbol<FreeListFunction>("libusb_free_device_list");
        GetDescriptor = Symbol<DescriptorFunction>("libusb_get_device_descriptor");
        GetBus = Symbol<AddressFunction>("libusb_get_bus_number");
        GetAddress = Symbol<AddressFunction>("libusb_get_device_address");
        Open = Symbol<OpenFunction>("libusb_open");
        Close = Symbol<CloseFunction>("libusb_close");
        Claim = Symbol<InterfaceFunction>("libusb_claim_interface");
        Release = Symbol<InterfaceFunction>("libusb_release_interface");
        BulkTransfer = Symbol<BulkFunction>("libusb_bulk_transfer");
    }

    internal InitFunction Init { get; }
    internal DeviceListFunction GetDeviceList { get; }
    internal FreeListFunction FreeDeviceList { get; }
    internal DescriptorFunction GetDescriptor { get; }
    internal AddressFunction GetBus { get; }
    internal AddressFunction GetAddress { get; }
    internal OpenFunction Open { get; }
    internal CloseFunction Close { get; }
    internal InterfaceFunction Claim { get; }
    internal InterfaceFunction Release { get; }
    internal BulkFunction BulkTransfer { get; }
    internal IntPtr Context { get; private set; }

    internal static Result<LibUsbNative, CaptureError> Load(string absolutePath)
    {
        if (!Path.IsPathFullyQualified(absolutePath))
        {
            return Result.Failure<LibUsbNative, CaptureError>(
                new CaptureError(
                    CaptureErrorCodes.InvalidArgument,
                    "An explicit native library path is required."
                )
            );
        }

        var library = NativeLibrary.Load(absolutePath);
        bool retained = false;
        try
        {
            var api = new LibUsbNative(library);
            if (api.Init(out var context) != 0)
            {
                return Result.Failure<LibUsbNative, CaptureError>(
                    new CaptureError(
                        CaptureErrorCodes.DeviceUnavailable,
                        "USB initialization failed."
                    )
                );
            }

            api.Context = context;
            retained = true;
            return api;
        }
        finally
        {
            if (!retained)
            {
                NativeLibrary.Free(library);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            await UsbEdge
                .RunAsync(() => ValueTask.FromResult(ReleaseLibrary()), CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private Result<bool, CaptureError> ReleaseLibrary()
    {
        try
        {
            exit(Context);
            Context = IntPtr.Zero;
            return true;
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    private T Symbol<T>(string name)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int InitFunction(out IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ExitFunction(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint DeviceListFunction(IntPtr context, out IntPtr devices);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void FreeListFunction(IntPtr devices, int unrefDevices);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int DescriptorFunction(IntPtr device, [Out] byte[] descriptor);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate byte AddressFunction(IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int OpenFunction(IntPtr device, out IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CloseFunction(IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int InterfaceFunction(IntPtr handle, int interfaceNumber);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int BulkFunction(
        IntPtr handle,
        byte endpoint,
        [In, Out] byte[] data,
        int length,
        out int transferred,
        uint timeout
    );
}
