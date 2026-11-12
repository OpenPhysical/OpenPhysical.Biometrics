// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

internal sealed class ScoringTestServices : IDisposable
{
    private readonly ServiceProvider services = new ServiceCollection()
        .AddFingerprintProcessing()
        .AddNfiq1FingerprintQuality()
        .AddNfiq2FingerprintQuality()
        .BuildServiceProvider();

    internal ScoringTestServices()
    {
        Engine = new(services.GetRequiredService<IFingerprintEngine>());
        Nfiq1 = new(services.GetRequiredService<IFingerprintQualityProvider>());
        Nfiq2 = new(services.GetRequiredService<IFingerprintUnifiedQualityProvider>());
        Processor = new(new FingerprintPreparationService(Engine, Nfiq1), Nfiq2);
    }

    internal CountingFingerprintEngine Engine { get; }
    internal CountingQualityProvider Nfiq1 { get; }
    internal CountingUnifiedQualityProvider Nfiq2 { get; }
    internal CaptureProcessor Processor { get; }
    internal (int Nfiq1, int Nfiq2, int Extractions) Counts =>
        (Nfiq1.Calls, Nfiq2.Calls, Engine.Extractions);

    internal static ImmutableArray<FingerCaptureCandidate> Candidates(CaptureFrame frame, int count)
    {
        var mask = Fs50ContactAnalysis.Analyze(frame.Pixels.AsSpan()).Value.Mask;
        return Enumerable
            .Range(0, count)
            .Select(index => new FingerCaptureCandidate(
                CaptureFrame
                    .Create(frame.Pixels.AsSpan(), index, frame.CapturedAt, frame.Provenance)
                    .Value,
                mask,
                index,
                0.1,
                mask.Count(value => value != 0),
                index
            ))
            .ToImmutableArray();
    }

    internal static FingerprintPreprocessingArtifact Artifact(
        CaptureFrame frame,
        int brightness,
        long revision
    ) =>
        FingerprintPreprocessing
            .Process(
                frame,
                Fs50ContactAnalysis.Analyze(frame.Pixels.AsSpan()).Value.ToProcessingMask().Value,
                FingerprintPreprocessingPolicy
                    .Create(claheEnabled: false, brightness: brightness, contrast: 0)
                    .Value,
                revision,
                CancellationToken.None
            )
            .Value;

    public void Dispose() => services.Dispose();
}
