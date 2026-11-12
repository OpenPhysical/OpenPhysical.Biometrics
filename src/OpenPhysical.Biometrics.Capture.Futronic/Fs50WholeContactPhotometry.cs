// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal static class Fs50WholeContactPhotometry
{
    internal static Result<Fs50WholeContactSpatialComparison, CaptureError> EvaluateObserved(
        CaptureFrame frame,
        Fs50EmptySensorReference reference,
        ReadOnlySpan<byte> contact,
        ReadOnlySpan<byte> feature,
        ReadOnlySpan<byte> observed,
        ReadOnlySpan<byte> filledObserved,
        Fs50WholeContactPolicy policy,
        CancellationToken token
    )
    {
        var selected = Evaluate(frame, reference, contact, feature, policy, token);
        if (selected.IsFailure)
            return selected.Error;
        return Evaluate(frame, reference, observed, filledObserved, policy, token)
            .Map(whole => new Fs50WholeContactSpatialComparison(selected.Value, whole));
    }

    internal static Result<Fs50WholeContactSpatialResult, CaptureError> Evaluate(
        CaptureFrame frame,
        Fs50EmptySensorReference reference,
        ReadOnlySpan<byte> contact,
        ReadOnlySpan<byte> feature,
        Fs50WholeContactPolicy policy,
        CancellationToken token
    )
    {
        using var grid = new Fs50ContactComparisonGrid();
        var filled = grid.Fill(frame.Pixels.AsSpan(), reference.Pixels.AsSpan(), contact, token);
        if (filled.IsFailure)
            return filled.Error;
        var observed = grid.Observe(policy, token);
        if (observed.IsFailure)
            return observed.Error;
        var finished = grid.Finish(contact, token);
        if (finished.IsFailure)
            return finished.Error;
        using var evidence = finished.Value;
        using var corroborated = new Fs50TemporaryPlane();
        _ = Fs50MaskMorphology.ExpandInto(
            evidence.Changed,
            policy.Evidence.Options.SupportedExpansion,
            corroborated.Buffer
        );
        return Fs50WholeContactExterior.Measure(
            contact,
            feature,
            corroborated.PixelsSpan,
            evidence.Static,
            policy.Options.MinimumUncorroboratedArea,
            token
        );
    }
}
