// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class CaptureAdjustmentControlBinding
{
    private bool restoring;
    internal ImageAdjustmentViewModel Value { get; } = new();

    internal CaptureAdjustmentControlBinding(Func<ImageAdjustmentSettings, Task> preview) =>
        Value.Changed += settings =>
        {
            if (!restoring)
                _ = preview(settings);
        };

    internal UnitResult<DesktopError> Restore(ImageAdjustmentSettings settings)
    {
        restoring = true;
        var restored = Value.Restore(settings);
        restoring = false;
        return restored;
    }
}
