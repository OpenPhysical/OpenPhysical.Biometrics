using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Pipeline;
using OpenPhysical.Biometrics.Infrastructure.Pipeline.Checkpoints;
using OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;
using OpenPhysical.Biometrics.Infrastructure.Services;

namespace OpenPhysical.Biometrics.Infrastructure.Tests;

/// <summary>Checks that operational logs never copy caller diagnostic payloads.</summary>
[TestFixture]
public sealed class PrivacyLoggingTests
{
    private const string Sensitive = "capture-identity-12345-sensitive-template";

    /// <summary>Checkpoint exceptions remain available to callers without entering logs.</summary>
    [Test]
    public async Task CheckpointFailuresDoNotLogExternalExceptionPayloads()
    {
        var qualityLog = new RecordingLogger<QualityApprovalCheckpoint>();
        var faceLog = new RecordingLogger<FaceSelectionCheckpoint>();
        var quality = await new FailingQualityCheckpoint(qualityLog).RequestDecisionAsync(
            default,
            PipelineContext.Create()
        );
        var face = new DetectedFace(FaceBox.Create(0, 0, 20, 20).Value, 0.9f);
        var selection = await new FailingFaceCheckpoint(faceLog).RequestDecisionAsync(
            new[] { face, face },
            PipelineContext.Create()
        );
        Assert.Multiple(() =>
        {
            Assert.That(quality.IsFailure, Is.True);
            Assert.That(selection.IsFailure, Is.True);
            Assert.That(qualityLog.Exceptions.Concat(faceLog.Exceptions), Is.Empty);
            Assert.That(
                string.Join("\n", qualityLog.Messages.Concat(faceLog.Messages)),
                Does.Not.Contain(Sensitive)
            );
            Assert.That(
                qualityLog.Events.Concat(faceLog.Events).Where(e => e.Id != 0),
                Is.Not.Empty
            );
        });
    }

    /// <summary>Service diagnostic results travel to the caller without entering logs.</summary>
    [Test]
    public async Task EncodingServiceFailuresDoNotLogExternalDiagnosticPayloads()
    {
        var service = Substitute.For<IJpeg2000Service>();
        service
            .EncodeAsync(
                Arg.Any<ImmutableArray<byte>>(),
                Arg.Any<Jpeg2000Options>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(Result.Failure<EncodedJpeg2000>(Sensitive)));
        var logger = new RecordingLogger<EncodingStep>();
        var data = BiometricData.FromBytes(
            new byte[] { 1 },
            BiometricDataType.FacialImage,
            BiometricFormat.Png
        );
        var result = await new EncodingStep(service, logger).ExecuteAsync(
            ProcessedBiometricData.FromBiometricData(data, default),
            PipelineContext.Create()
        );
        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(Sensitive));
            Assert.That(logger.Exceptions, Is.Empty);
            Assert.That(string.Join("\n", logger.Messages), Does.Not.Contain(Sensitive));
            Assert.That(logger.Events.Any(e => e.Id != 0), Is.True);
        });
    }

    private sealed class FailingQualityCheckpoint(ILogger<QualityApprovalCheckpoint> logger)
        : QualityApprovalCheckpoint(logger)
    {
        protected override Task<CheckpointResult> RequestQualityDecisionAsync(
            QualityMetrics qualityMetrics,
            IPipelineContext context,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException(Sensitive);
    }

    private sealed class FailingFaceCheckpoint(ILogger<FaceSelectionCheckpoint> logger)
        : FaceSelectionCheckpoint(logger)
    {
        protected override Task<CheckpointResult> RequestFaceSelectionAsync(
            IReadOnlyList<DetectedFace> detectedFaces,
            IPipelineContext context,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException(Sensitive);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        internal List<string> Messages { get; } = [];
        internal List<Exception> Exceptions { get; } = [];
        internal List<EventId> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Messages.Add(formatter(state, exception));
            Events.Add(eventId);
            if (exception is not null)
                Exceptions.Add(exception);
        }
    }
}
