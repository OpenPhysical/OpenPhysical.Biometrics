// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

/// <summary>Address of one current USB enumeration, not a permanent device identity.</summary>
/// <param name="VendorId">USB vendor identifier.</param>
/// <param name="ProductId">USB product identifier.</param>
/// <param name="Bus">Current USB bus.</param>
/// <param name="Address">Current device address; may change after reconnect.</param>
public sealed record UsbDeviceInfo(ushort VendorId, ushort ProductId, byte Bus, byte Address)
{
    /// <summary>Canonical current-device lease key shared by capture drivers.</summary>
    public string DeviceId =>
        FormattableString.Invariant($"usb:{VendorId:x4}:{ProductId:x4}:{Bus}:{Address}");
}
