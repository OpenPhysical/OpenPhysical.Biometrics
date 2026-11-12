// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class CaptureEditConfiguration(CaptureCoordinator coordinator)
{
    private readonly CapturePolicySettings previousPolicy = coordinator.Policy;
    private readonly ImageAdjustmentSettings previousImage = Image(
        coordinator.Images.Snapshot().Policy
    );

    internal static ImageAdjustmentSettings Image(
        OpenPhysical.Biometrics.Capture.FingerprintPreprocessingPolicy policy
    ) =>
        ImageAdjustmentSettings
            .Create(policy.ClaheEnabled, policy.Brightness, policy.Contrast)
            .Value;

    internal UnitResult<DesktopError> Configure(
        CapturedFingerprint source,
        ImageAdjustmentSettings image
    )
    {
        var evidence = source.Processing.Value;
        var actual = evidence.Artifact.Policy;
        var policy = EnhancementSettings
            .Create(actual.TileSize, actual.ClipLimit, actual.Blend)
            .Bind(enhancement =>
                CapturePolicySettings.Create(
                    enhancement,
                    evidence.ContactDiagnostics.Policy.Options with
                    {
                        InnerGuard = evidence.Contour.Policy.Options.ModifiedBoundaryGuard,
                    },
                    previousPolicy.EmptySensorOptions,
                    previousPolicy.MinimumNfiq1Gain,
                    previousPolicy.MinimumNfiq2Gain,
                    evidence.Contour.Policy.Options.ContextMargin
                )
            )
            .Bind(value => value.WithPresentation(previousPolicy.Presentation))
            .Bind(value => value.WithWholeContact(previousPolicy.WholeContactOptions));
        if (policy.IsFailure)
            return UnitResult.Failure(policy.Error);
        coordinator.ConfigurePolicy(policy.Value);
        coordinator.Images.Configure(image, policy.Value.Enhancement);
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Restore()
    {
        coordinator.ConfigurePolicy(previousPolicy);
        coordinator.Images.Configure(previousImage, previousPolicy.Enhancement);
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> RestoreAsync(
        Func<Task<UnitResult<DesktopError>>> drain,
        Func<UnitResult<DesktopError>> complete
    )
    {
        // A discarded draft's processing failure does not prevent restoring accepted controls.
        await drain();
        var restored = Restore();
        var completed = complete();
        return restored.IsFailure ? restored : completed;
    }
}
