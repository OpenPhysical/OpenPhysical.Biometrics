// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Imaging;

internal sealed class FaceTestCameraProvider(bool deferOpen = false) : ICameraProvider
{
    private readonly TaskCompletionSource released = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    internal TaskCompletionSource Opening { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal CameraSource Source { get; init; } =
        new("test-camera", "Public fixture camera", [new(808, 1024, 30, 1, "RGB24")]);
    internal Maybe<CameraFrame> LastFrame { get; private set; }
    internal float FixtureRotation { get; init; }
    internal bool DeferFirstFrame { get; init; }
    internal bool EmptyImage { get; init; }
    internal int OpenCount { get; private set; }
    internal int StopCount { get; private set; }
    private Maybe<Session> current;

    internal UnitResult<CameraError> Unplug() =>
        current.Match(value => value.Unplug(), UnitResult.Success<CameraError>);

    internal UnitResult<CameraError> FailRead(CameraError error) =>
        current.Match(value => value.FailRead(error), UnitResult.Success<CameraError>);

    internal UnitResult<CameraError> PublishFirst() =>
        current.Match(value => value.PublishFirst(), UnitResult.Success<CameraError>);

    internal UnitResult<CameraError> PublishNext()
    {
        var previous = LastFrame.Value;
        var next = new CameraFrame(
            previous.Image.Clone().Value,
            previous.Source,
            previous.Mode,
            previous.Sequence + 1,
            previous.CapturedAt.AddMilliseconds(33),
            previous.DeviceTimestamp
        );
        LastFrame = next;
        return current.Value.Publish(next);
    }

    internal bool ReleaseOpen() => released.TrySetResult();

    public Task<Result<ImmutableArray<CameraSource>, CameraError>> EnumerateAsync(
        CancellationToken token
    ) => Task.FromResult(Result.Success<ImmutableArray<CameraSource>, CameraError>([Source]));

    public async Task<Result<ICameraSession, CameraError>> OpenAsync(
        string sourceId,
        CameraMode mode,
        CancellationToken token
    )
    {
        OpenCount++;
        Opening.TrySetResult();
        if (deferOpen)
            await released.Task;
        var image = Image(mode).Value;
        if (FixtureRotation != 0)
            image
                .Rotate(FixtureRotation)
                .Bind(value => value.Resize(mode.Width, mode.Height))
                .Should()
                .Succeed();
        var frame = new CameraFrame(
            image,
            Source,
            mode,
            OpenCount,
            DateTimeOffset.UnixEpoch,
            TimeSpan.Zero
        );
        LastFrame = frame;
        var session = new Session(frame, () => StopCount++, DeferFirstFrame);
        current = session;
        return Result.Success<ICameraSession, CameraError>(session);
    }

    private Result<RgbImage, ImagingError> Image(CameraMode mode) =>
        EmptyImage
            ? RgbImage.Create(mode.Width, mode.Height, System.Drawing.Color.White)
            : RgbImageDecoder.Load(
                Path.Combine(
                    NUnit.Framework.TestContext.CurrentContext.TestDirectory,
                    "Fixtures",
                    "face-bush.jpg"
                )
            );

    private sealed class Session : ICameraSession
    {
        private readonly CameraFrameBuffer queue = new();
        private readonly Action stopped;
        private bool closed;
        private bool disconnected;
        private Maybe<CameraError> failure;
        private Maybe<CameraFrame> initial;

        internal Session(CameraFrame frame, Action stopped, bool deferFirstFrame)
        {
            if (deferFirstFrame)
                initial = frame;
            else
                queue.Publish(frame);
            this.stopped = stopped;
        }

        internal UnitResult<CameraError> PublishFirst()
        {
            var next = initial;
            initial = default;
            return next.ToResult(
                    new CameraError(CameraErrorCodes.InvalidFrame, "No pending fixture frame.")
                )
                .Bind(queue.Publish);
        }

        public async Task<Result<CameraFrame, CameraError>> ReadAsync(CancellationToken token)
        {
            var read = await queue.ReadAsync(token);
            if (failure.HasValue && read.IsFailure)
                return failure.Value;
            return disconnected && read.IsFailure
                ? new CameraError(CameraErrorCodes.Unavailable, "The camera was disconnected.")
                : read;
        }

        internal UnitResult<CameraError> Publish(CameraFrame frame) => queue.Publish(frame);

        internal UnitResult<CameraError> Unplug()
        {
            disconnected = true;
            return queue.Complete();
        }

        internal UnitResult<CameraError> FailRead(CameraError error)
        {
            failure = error;
            return queue.Complete();
        }

        public Task<UnitResult<CameraError>> StopAsync(CancellationToken token)
        {
            if (!closed)
            {
                closed = true;
                stopped();
                initial.Tap(value => value.Dispose());
                initial = default;
                queue.Complete();
            }
            return Task.FromResult(UnitResult.Success<CameraError>());
        }

        public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
    }
}
