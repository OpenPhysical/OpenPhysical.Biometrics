// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ControlledInspectionDependencies
    : ObservableObject,
        ICapturedFingerprintSource,
        IFingerprintEngine,
        ITemplateStorage
{
    private readonly TaskCompletionSource<bool> release = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    internal TaskCompletionSource<bool> Entered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Maybe<CapturedFingerprint> captured = StorageFixtures.Capture(width: 128, height: 128);
    public Maybe<CapturedFingerprint> Captured => captured;
    private Maybe<DisplayedFingerprintFrame> liveFrame;
    public Maybe<DisplayedFingerprintFrame> LiveFrame => liveFrame;

    internal UnitResult<DesktopError> SetLiveFrame(Maybe<DisplayedFingerprintFrame> value)
    {
        if (SetProperty(ref liveFrame, value))
            OnPropertyChanged(nameof(LiveFrame));
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> SetCapture(Maybe<CapturedFingerprint> value)
    {
        if (SetProperty(ref captured, value))
            OnPropertyChanged(nameof(Captured));
        return UnitResult.Success<DesktopError>();
    }

    internal TemplateDocument Document { get; set; } =
        DocumentFor(Fingerprints.FingerPosition.RightIndex);

    internal bool Release() => release.TrySetResult(true);

    internal Maybe<FingerprintTemplate> ComparedProbe { get; private set; }

    public FingerprintTemplate Extract(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int dotsPerInch
    ) => new(width, height, [], "unused extraction");

    public int Match(FingerprintTemplate probe, FingerprintTemplate gallery)
    {
        ComparedProbe = probe;
        Entered.TrySetResult(true);
        release.Task.GetAwaiter().GetResult();
        return 42;
    }

    public Task<Result<TemplateDocument, DesktopError>> LoadAsync(
        string path,
        CancellationToken cancellationToken
    ) => Task.FromResult(Result.Success<TemplateDocument, DesktopError>(Document));

    public Task<Result<string, DesktopError>> SaveAsync(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        string folder,
        CancellationToken cancellationToken
    ) => Task.FromResult(Result.Failure<string, DesktopError>(new("Unused", "No save requested.")));

    internal static TemplateDocument DocumentFor(Fingerprints.FingerPosition finger) =>
        new(
            UnsignedTemplateService
                .Create([StorageFixtures.Capture(finger, 128, 128)], new(1, 0, 0))
                .Value,
            []
        );
}
