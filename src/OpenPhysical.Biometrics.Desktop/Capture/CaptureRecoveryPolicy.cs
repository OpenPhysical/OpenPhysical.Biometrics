// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

internal sealed class CaptureRecoveryPolicy
{
    private int transfers;

    internal bool Retry(DesktopError error)
    {
        if (error.Code.EndsWith("DeviceUnavailable", StringComparison.Ordinal))
            return true;
        if (
            error.Code.EndsWith("TransferIncomplete", StringComparison.Ordinal)
            || error.Code.EndsWith("TransferTimedOut", StringComparison.Ordinal)
            || error.Code.EndsWith("Cancelled", StringComparison.Ordinal)
        )
            return transfers++ == 0;
        return false;
    }

    internal bool Reset()
    {
        transfers = 0;
        return true;
    }
}
