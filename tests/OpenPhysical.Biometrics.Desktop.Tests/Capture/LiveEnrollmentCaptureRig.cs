// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.ComponentModel;
using System.Threading.Channels;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class LiveEnrollmentCaptureRig : IAsyncDisposable
{
    private readonly ProcessingCaptureRig capture = new();
    private readonly InspectionViewModel inspection;
    private readonly Channel<CapturedFingerprint> completed =
        Channel.CreateUnbounded<CapturedFingerprint>();
    private readonly PropertyChangedEventHandler changed;

    internal LiveEnrollmentCaptureRig()
    {
        inspection = new InspectionViewModel(
            new TemplateStorage(),
            new DesktopFilePicker(),
            View,
            new TemplateComparison(new FingerprintComparisonService(capture.Services.Engine)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );
        Export = new ExportViewModel(
            View,
            new ControlledExportStorage(),
            EnrollmentTestSuggestions.IndexPair(),
            inspection
        );
        changed = (_, change) =>
        {
            if (change.PropertyName == nameof(CaptureViewModel.Captured) && View.Captured.HasValue)
                _ = completed.Writer.TryWrite(View.Captured.Value);
        };
        View.PropertyChanged += changed;
    }

    internal CaptureViewModel View => capture.View;
    internal ControlledCaptureDriver Driver => capture.Driver;
    internal ControlledCaptureClock Clock => capture.Clock;
    internal ExportViewModel Export { get; }

    internal Task<ControlledCaptureRequest> StartAsync() => capture.StartAsync();

    internal Task<CapturedFingerprint> NextCaptureAsync() =>
        completed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    public async ValueTask DisposeAsync()
    {
        View.PropertyChanged -= changed;
        await Export.StopAsync();
        await inspection.StopAsync();
        await capture.DisposeAsync();
    }
}
