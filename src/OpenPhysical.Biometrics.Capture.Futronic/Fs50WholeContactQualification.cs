// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Session-only spatial image-admission evidence; callers serialize observation and clearing.</summary>
public sealed class Fs50WholeContactQualification : IDisposable
{
    private readonly Fs50WholeContactPolicy policy;
    private readonly Fs50WholeContactAssessment assessment;
    private readonly Fs50WholeContactSequence sequence = new();
    private readonly Fs50WholeContactGeometryPairCache geometry = new();

    private Fs50WholeContactQualification(Fs50WholeContactPolicy policy, TimeProvider clock)
    {
        this.policy = policy;
        assessment = new Fs50WholeContactAssessment(policy, clock);
    }

    /// <summary>Creates bounded qualification state without operating a sensor.</summary>
    /// <param name="policy">Validated original qualification choices.</param>
    /// <param name="clock">Monotonic observation clock.</param>
    /// <returns>Validated session state or invalid-argument failure.</returns>
    public static Result<Fs50WholeContactQualification, CaptureError> Create(
        Fs50WholeContactPolicy policy,
        TimeProvider clock
    ) =>
        policy is not null && clock is not null
            ? new Fs50WholeContactQualification(policy, clock)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Whole-contact policy and clock are required."
            );

    /// <summary>Qualifies exact original pixels against compatible reference evidence or anchored geometry.</summary>
    /// <param name="frame">Exact corrected acquisition.</param>
    /// <param name="contact">Original contact evidence from that frame.</param>
    /// <param name="sensor">Session-only empty-reference observation.</param>
    /// <param name="sessionId">Current open scanner-session identity.</param>
    /// <param name="cancellationToken">Cancellation prevents publishing a partial proof.</param>
    /// <returns>Frozen frame-associated evidence, including explicit rejection or unavailability.</returns>
    public Result<Fs50WholeContactProof, CaptureError> Observe(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        Fs50EmptySensorObservation sensor,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        if (sensor is null)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Empty-reference observation is required."
            );
        var prepared = Fs50WholeContactInput.Create(
            frame,
            contact,
            policy,
            sessionId,
            geometry,
            cancellationToken
        );
        if (prepared.IsFailure)
            return Invalidate(prepared.Error);
        var input = prepared.Value;
        var repeated = sequence.Before(input);
        if (repeated.IsFailure)
            return Invalidate(repeated.Error);
        if (repeated.Value.HasValue)
            return repeated.Value.Value;
        if (assessment.Track(input.Key))
            geometry.Clear();
        return Complete(input, sensor, cancellationToken);
    }

    private Result<Fs50WholeContactProof, CaptureError> Complete(
        Fs50WholeContactInput input,
        Fs50EmptySensorObservation sensor,
        CancellationToken cancellationToken
    )
    {
        var result = assessment
            .Inspect(input, sensor, cancellationToken)
            .Check(_ => CheckCancellation(cancellationToken))
            .Map(proof => sequence.Remember(input.Key, proof));
        if (result.IsFailure)
            geometry.Clear();
        return result;
    }

    /// <summary>Discards reference compatibility, anchored geometry and consecutive removal state.</summary>
    /// <returns>Successful release of bounded session state.</returns>
    public UnitResult<CaptureError> Clear()
    {
        assessment.Clear();
        sequence.Clear();
        geometry.Clear();
        return UnitResult.Success<CaptureError>();
    }

    private CaptureError Invalidate(CaptureError error)
    {
        geometry.Clear();
        return error;
    }

    private UnitResult<CaptureError> CheckCancellation(CancellationToken token)
    {
        if (!token.IsCancellationRequested)
            return UnitResult.Success<CaptureError>();
        geometry.Clear();
        return Fs50MaskErrors.Cancelled;
    }

    /// <summary>Discards session-only geometry; already-issued immutable proofs remain valid for their original frames.</summary>
    public void Dispose() => _ = Clear();
}
