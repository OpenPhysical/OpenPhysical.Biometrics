// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class EnrollmentOperatorContext : IAsyncDisposable
{
    private readonly ScoringTestServices services = new();

    internal EnrollmentOperatorContext(
        ITemplateSessionStorage storage,
        EnrollmentFingerSuggestions suggestions
    )
    {
        var driver = new ControlledCaptureDriver { Available = false };
        var clock = new ControlledCaptureClock();
        var dispatcher = new DesktopPreviewDispatcher();
        var coordinator = new CaptureCoordinator(
            driver.Registry,
            dispatcher,
            clock,
            NullLogger<CaptureCoordinator>.Instance
        );
        Capture = new CaptureViewModel(coordinator, services.Processor, dispatcher, clock)
        {
            AutoCapture = false,
            AutomaticExposure = false,
        };
        Inspection = new InspectionViewModel(
            new TemplateStorage(),
            new DesktopFilePicker(),
            Capture,
            new TemplateComparison(new FingerprintComparisonService(services.Engine)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );
        Export = new ExportViewModel(Capture, storage, suggestions, Inspection);
    }

    internal CaptureViewModel Capture { get; }
    internal InspectionViewModel Inspection { get; }
    internal ExportViewModel Export { get; }
    internal (int Nfiq1, int Nfiq2, int Extractions) Counts => services.Counts;

    internal Task<UnitResult<DesktopError>> PresentAsync(CapturedFingerprint capture) =>
        Capture.ReviewAcceptedAsync(capture);

    internal async Task<UnitResult<DesktopError>> AcceptAsync(CapturedFingerprint capture)
    {
        var presented = await PresentAsync(capture);
        if (presented.IsFailure)
            return presented;
        await Export.Enrollment.AcceptCommand.ExecuteAsync(default);
        return UnitResult.Success<DesktopError>();
    }

    internal CapturedFingerprint NativeCapture(
        FingerPosition finger = FingerPosition.RightIndex,
        bool modified = false
    )
    {
        var policy = CapturePolicySettings.Default;
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var selected = services
            .Processor.EvaluateFrame(
                frame,
                new(
                    policy.Enhancement.For(
                        modified
                            ? ImageAdjustmentSettings.Default
                            : ImageAdjustmentSettings.Original
                    ),
                    policy.Evidence,
                    policy.Contour,
                    1,
                    CaptureProcessingInput.DeclaredOfflineImage
                ),
                CancellationToken.None
            )
            .Value;
        return new CaptureAcquisitionPolicy(policy, 1).ApplyTo(
            services.Processor.Extract(selected, finger, default, CancellationToken.None).Value
        );
    }

    public async ValueTask DisposeAsync()
    {
        await Export.StopAsync();
        await Inspection.StopAsync();
        await Capture.StopAsync();
        services.Dispose();
    }
}
