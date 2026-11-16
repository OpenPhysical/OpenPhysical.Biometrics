// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPhysical.Biometrics.Capture.Usb;

/// <summary>Registers direct capture without opening USB hardware.</summary>
public static class FutronicServiceCollectionExtensions
{
    /// <summary>Adds the FS50 driver using a configured USB provider and clock.</summary>
    /// <param name="services">Application services; register IUsbDeviceProvider separately.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddFs50Capture(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IFingerprintCaptureDriver, Fs50CaptureDriver>()
        );
        return services;
    }
}
