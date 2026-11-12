// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal sealed class Fs50WholeContactAssessment
{
    private readonly Fs50WholeContactPolicy policy;
    private readonly Fs50WholeContactLifetime lifetime;
    private Maybe<Fs50WholeContactAnchor> anchor;
    private Maybe<Fs50EmptySensorKey> rejected;

    internal Fs50WholeContactAssessment(Fs50WholeContactPolicy policy, TimeProvider clock)
    {
        this.policy = policy;
        lifetime = new Fs50WholeContactLifetime(policy, clock);
    }

    internal bool Track(Fs50EmptySensorKey key)
    {
        if (rejected.HasValue && !rejected.Value.SameSession(key))
            rejected = Maybe<Fs50EmptySensorKey>.None;
        var changed = lifetime.Track(key);
        if (changed)
            anchor = Maybe<Fs50WholeContactAnchor>.None;
        return changed;
    }

    internal bool Clear()
    {
        anchor = Maybe<Fs50WholeContactAnchor>.None;
        rejected = Maybe<Fs50EmptySensorKey>.None;
        return lifetime.Clear();
    }

    internal Result<Fs50WholeContactProof, CaptureError> Inspect(
        Fs50WholeContactInput input,
        Fs50EmptySensorObservation sensor,
        CancellationToken cancellationToken
    )
    {
        if (!input.SingleContact)
            return NoContact(input, sensor);
        lifetime.Contact();
        if (sensor.Reference.HasValue && sensor.Reference.Value.Key == input.Key)
            return Matched(input, sensor.Reference.Value, cancellationToken);
        if (rejected.HasValue)
            return Uncompared(input, Fs50WholeContactDecision.Unavailable);
        if (anchor.HasValue)
            return Anchored(input, cancellationToken);
        return Fs50WholeContactProofFactory.Spatial(input, policy);
    }

    private Fs50WholeContactProof NoContact(
        Fs50WholeContactInput input,
        Fs50EmptySensorObservation sensor
    )
    {
        if (lifetime.ObserveEmpty(input, sensor))
        {
            anchor = Maybe<Fs50WholeContactAnchor>.None;
            rejected = Maybe<Fs50EmptySensorKey>.None;
        }
        return Uncompared(input, Fs50WholeContactDecision.NoContact);
    }

    private Result<Fs50WholeContactProof, CaptureError> Matched(
        Fs50WholeContactInput input,
        Fs50EmptySensorReference reference,
        CancellationToken token
    ) =>
        Fs50WholeContactPhotometry
            .EvaluateObserved(
                input.Frame,
                reference,
                input.Contact,
                input.Feature,
                input.Observed,
                input.FilledObserved,
                policy,
                token
            )
            .Bind(spatial => Remember(input, reference, spatial));

    private Result<Fs50WholeContactProof, CaptureError> Remember(
        Fs50WholeContactInput input,
        Fs50EmptySensorReference reference,
        Fs50WholeContactSpatialComparison spatial
    )
    {
        var decision = Fs50WholeContactProofFactory.Decision(spatial, policy);
        if (decision == Fs50WholeContactDecision.QualifiedGeometry)
            decision = Fs50WholeContactDecision.QualifiedReference;
        decision = Fs50WholeContactProofFactory.ForCapture(decision, input);
        var proof = Fs50WholeContactProofFactory.Create(
            input,
            policy,
            decision,
            spatial.Contact.Metrics,
            spatial.Observed.Metrics,
            reference,
            default
        );
        _ = TrackRejection(input, decision, matched: true);
        if (!proof.Qualified)
            return proof;
        return Fs50WholeContactAnchor
            .Create(input, proof.Record)
            .Map(value =>
            {
                anchor = value;
                return proof;
            });
    }

    private Result<Fs50WholeContactProof, CaptureError> Anchored(
        Fs50WholeContactInput input,
        CancellationToken token
    ) =>
        anchor
            .Value.Evaluate(input, policy, token)
            .Map(observed =>
            {
                var decision = Fs50WholeContactProofFactory.ForCapture(
                    Fs50WholeContactProofFactory.Decision(
                        observed.Spatial,
                        policy,
                        observed.Compatible
                    ),
                    input
                );
                var proof = Fs50WholeContactProofFactory.Create(
                    input,
                    policy,
                    decision,
                    observed.Spatial.Contact.Metrics,
                    observed.Spatial.Observed.Metrics,
                    default,
                    anchor.Value.Digest
                );
                _ = TrackRejection(input, decision, matched: false);
                return proof;
            });

    private bool TrackRejection(
        Fs50WholeContactInput input,
        Fs50WholeContactDecision decision,
        bool matched
    )
    {
        if (
            decision
            is Fs50WholeContactDecision.StaticExterior
                or Fs50WholeContactDecision.UnresolvedExterior
        )
        {
            rejected = input.Key;
            anchor = Maybe<Fs50WholeContactAnchor>.None;
        }
        else if (matched)
            rejected = Maybe<Fs50EmptySensorKey>.None;
        return rejected.HasValue;
    }

    private Fs50WholeContactProof Uncompared(
        Fs50WholeContactInput input,
        Fs50WholeContactDecision decision
    ) =>
        Fs50WholeContactProofFactory.Create(
            input,
            policy,
            decision,
            Fs50WholeContactProofFactory.Unknown(input.Contact),
            Fs50WholeContactProofFactory.Unknown(input.Observed),
            default,
            default
        );
}
