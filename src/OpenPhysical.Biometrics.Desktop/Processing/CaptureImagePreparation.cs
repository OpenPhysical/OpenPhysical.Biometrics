// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal sealed class CaptureImagePreparation
{
    private readonly object sync = new();
    private FingerprintPreprocessingPolicy policy = CapturePolicySettings.Default.Enhancement.For(
        ImageAdjustmentSettings.Default
    );
    private long revision;
    private readonly PreviewImagePreparation preview = new();

    internal Result<FingerprintPreprocessingArtifact, DesktopError> PreparePreview(
        CaptureFrame frame,
        (FingerprintPreprocessingPolicy Policy, long Revision) settings,
        CancellationToken token
    ) => preview.Prepare(frame, settings, token);

    internal (FingerprintPreprocessingPolicy Policy, long Revision) Snapshot()
    {
        lock (sync)
            return (policy, revision);
    }

    internal long Configure(ImageAdjustmentSettings settings, EnhancementSettings enhancement)
    {
        lock (sync)
        {
            var next = enhancement.For(settings);
            if (next != policy)
            {
                policy = next;
                revision++;
            }
            return revision;
        }
    }

    internal Result<FingerprintPreprocessingArtifact, DesktopError> Prepare(
        CaptureFrame frame,
        FingerprintProcessingMask mask,
        CancellationToken token
    ) => Prepare(frame, mask, Snapshot(), token);

    internal Result<FingerprintPreprocessingArtifact, DesktopError> Prepare(
        CaptureFrame frame,
        FingerprintProcessingMask mask,
        (FingerprintPreprocessingPolicy Policy, long Revision) current,
        CancellationToken token
    ) =>
        FingerprintPreprocessing
            .Process(frame, mask, current.Policy, current.Revision, token)
            .MapError(error => new DesktopError(error.Code, error.Message));

    internal bool IsCurrent(FingerprintPreprocessingArtifact artifact) =>
        artifact.ProcessingRevision == Snapshot().Revision;

    internal bool IsCurrentPreview(Maybe<DisplayedFingerprintFrame> frame) =>
        frame.Bind(value => value.Processing).Match(IsCurrent, () => true);

    internal long Invalidate()
    {
        lock (sync)
            return ++revision;
    }
}
