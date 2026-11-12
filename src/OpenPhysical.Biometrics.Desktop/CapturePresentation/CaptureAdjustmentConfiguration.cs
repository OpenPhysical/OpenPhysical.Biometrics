// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

internal static class CaptureAdjustmentConfiguration
{
    internal static (long Previous, long Revision, bool NewMask, bool NewReadiness) Apply(
        CaptureCoordinator coordinator,
        CaptureAutomation automation,
        ImageAdjustmentSettings settings,
        CapturePolicySettings policy
    )
    {
        var previous = coordinator.Images.Snapshot().Revision;
        var newMask =
            coordinator.Policy.EvidenceOptions != policy.EvidenceOptions
            || coordinator.Policy.Contour.Digest != policy.Contour.Digest;
        var newReadiness = coordinator.Policy.WholeContact.Digest != policy.WholeContact.Digest;
        coordinator.ConfigurePolicy(policy);
        if (newReadiness || newMask)
            coordinator.Images.Invalidate();
        if (newReadiness)
        {
            coordinator.InvalidatePreview();
            automation.RestartTiming();
        }
        automation.ConfigurePolicy(policy.Presentation);
        return (
            previous,
            coordinator.Images.Configure(settings, policy.Enhancement),
            newMask,
            newReadiness
        );
    }
}
