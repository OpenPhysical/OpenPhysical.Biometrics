// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.ComponentModel;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class EnrollmentAdjustmentContext : IAsyncDisposable
{
    private readonly EnrollmentOperatorContext context;
    internal ControlledExportStorage Storage { get; }
    internal CaptureViewModel Capture => context.Capture;
    internal InspectionViewModel Inspection => context.Inspection;
    internal IFingerprintEnrollment Fingers => context.Export.Enrollment.Fingerprints;
    internal CaptureAdjustmentEditorViewModel Editor => Fingers.Adjustment;
    internal ExportViewModel Export => context.Export;
    internal CapturedFingerprint First { get; }
    internal CapturedFingerprint Second { get; }
    internal CapturedFingerprint Pending => IsSecond ? Second : First;
    internal bool IsSecond { get; }
    internal string PendingProgress =>
        IsSecond ? "1 of 2 fingerprints accepted" : "0 of 2 fingerprints accepted";
    internal (int Nfiq1, int Nfiq2, int Extractions) Counts => context.Counts;
    internal (int Nfiq1, int Nfiq2, int Extractions) EntryCounts { get; private set; }

    private EnrollmentAdjustmentContext(bool second, bool recorded, bool modified)
    {
        IsSecond = second;
        Storage = new ControlledExportStorage();
        Storage.Completed.SetResult(Result.Success<string, DesktopError>("saved.fmr"));
        context = new(Storage, EnrollmentTestSuggestions.IndexPair());
        Export.Enrollment.Person.Name = "Draft test";
        First = context.NativeCapture(FingerPosition.RightIndex, modified);
        var value = recorded
            ? SyntheticRecordedQualificationFixtures.Capture()
            : context.NativeCapture(FingerPosition.LeftIndex, modified);
        Second = value with
        {
            Finger = FingerPosition.LeftIndex,
            View = value.View with { FingerPosition = (byte)FingerPosition.LeftIndex },
        };
    }

    internal static async Task<EnrollmentAdjustmentContext> Create(
        bool second = true,
        bool recorded = false,
        bool modified = false,
        bool open = true
    )
    {
        var value = new EnrollmentAdjustmentContext(second, recorded, modified);
        if (second)
            (await value.context.AcceptAsync(value.First)).Should().Succeed();
        (await value.context.PresentAsync(value.Pending)).Should().Succeed();
        value.EntryCounts = value.Counts;
        value.Fingers.CanAdjustCapture.Should().BeTrue();
        if (open)
            await value.Fingers.AdjustCaptureCommand.ExecuteAsync(default);
        return value;
    }

    internal async Task PreviewAsync(Action change)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler handler = (_, _) =>
        {
            if (!Editor.IsBusy)
                ready.TrySetResult();
        };
        Editor.PropertyChanged += handler;
        try
        {
            change();
            if (!Editor.IsBusy)
                ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            Editor.PropertyChanged -= handler;
        }
    }

    internal async Task AcceptPendingAsync(CapturedFingerprint expected)
    {
        Fingers.Progress.Should().Be(PendingProgress);
        Fingers.ActiveAccepted.Should().BeFalse();
        await Fingers.AcceptCommand.ExecuteAsync(default);
        if (IsSecond)
        {
            await Export.SaveCommand.ExecuteAsync(default);
            Storage.Saves.Should().BeGreaterThan(0, Export.Status);
            Storage.Accepted.Should().Equal(First, expected);
            await Fingers.SecondarySlotCommand.ExecuteAsync(default);
        }
        else
            await Fingers.PrimarySlotCommand.ExecuteAsync(default);
        Capture.Captured.Value.Should().BeSameAs(expected);
    }

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
