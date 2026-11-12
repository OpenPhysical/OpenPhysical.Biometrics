// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using System.Threading.Channels;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ControlledCaptureDriver : IFingerprintCaptureDriver
{
    private readonly Channel<ControlledCaptureRequest> requests =
        Channel.CreateUnbounded<ControlledCaptureRequest>();
    private int available = 1;

    internal ControlledCaptureDriver()
    {
        Device = CaptureDeviceDescriptor
            .Create(
                Guid.NewGuid().ToString(),
                DriverId,
                "FS50 fixture",
                Maybe<string>.None,
                CaptureCapabilities.Create(0, 255, false, true).Value
            )
            .Value;
    }

    public string DriverId => "futronic-fs50";

    internal CaptureDeviceDescriptor Device { get; }

    internal int OpenCount { get; private set; }

    internal int DisposeCount { get; private set; }

    internal bool Available
    {
        get => Volatile.Read(ref available) != 0;
        set => Volatile.Write(ref available, value ? 1 : 0);
    }

    internal TaskCompletionSource<bool> Discovered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal CaptureDriverRegistry Registry =>
        CaptureDriverRegistry
            .Create(ImmutableArray.Create<IFingerprintCaptureDriver>(this), TimeProvider.System)
            .Value;

    internal Task<ControlledCaptureRequest> NextAsync() =>
        requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

    public Task<Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>> DiscoverAsync(
        CancellationToken cancellationToken
    )
    {
        var devices = Available
            ? ImmutableArray.Create(Device)
            : ImmutableArray<CaptureDeviceDescriptor>.Empty;
        _ = Discovered.TrySetResult(true);
        return Task.FromResult(
            Result.Success<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>(devices)
        );
    }

    public Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        string deviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken
    )
    {
        OpenCount++;
        return Task.FromResult(
            Result.Success<IFingerprintCaptureSession, CaptureError>(new Session(this, settings))
        );
    }

    private sealed class Session(ControlledCaptureDriver owner, CaptureSettings initial)
        : IFingerprintCaptureSession
    {
        private long sequence;
        private CaptureSettings settings = initial;

        public string DeviceId => owner.Device.DeviceId;

        public async Task<Result<CaptureFrame, CaptureError>> CaptureAsync(CancellationToken token)
        {
            var request = new ControlledCaptureRequest(owner.Device, settings, sequence++, token);
            using var registration = token.Register(() => request.Cancel());
            _ = owner.requests.Writer.TryWrite(request);
            return await request.Task;
        }

        public Task<UnitResult<CaptureError>> ApplySettingsAsync(
            CaptureSettings next,
            CancellationToken cancellationToken
        )
        {
            settings = next;
            return Task.FromResult(UnitResult.Success<CaptureError>());
        }

        public ValueTask DisposeAsync()
        {
            owner.DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
