// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal sealed class Fs50WholeContactSequence
{
    private Maybe<(Fs50EmptySensorKey Key, Fs50WholeContactProof Proof)> previous;

    internal Result<Maybe<Fs50WholeContactProof>, CaptureError> Before(Fs50WholeContactInput input)
    {
        if (previous.HasNoValue || !previous.Value.Key.SameSession(input.Key))
            return Maybe<Fs50WholeContactProof>.None;
        var identity = previous.Value.Proof.Record.Identity;
        if (input.Frame.Sequence > identity.Sequence)
            return Maybe<Fs50WholeContactProof>.None;
        if (
            input.Frame.Sequence == identity.Sequence
            && input.Frame.CapturedAt == identity.CapturedAt
            && input.Region.Diagnostics.SourceDigest == identity.SourceDigest
            && previous.Value.Key == input.Key
        )
            return Maybe.From(previous.Value.Proof);
        return new CaptureError(
            CaptureErrorCodes.InvalidFrame,
            "A scanner-session sequence was reused or regressed with different original acquisition identity."
        );
    }

    internal Fs50WholeContactProof Remember(Fs50EmptySensorKey key, Fs50WholeContactProof proof)
    {
        previous = (key, proof);
        return proof;
    }

    internal bool Clear()
    {
        previous = Maybe<(Fs50EmptySensorKey, Fs50WholeContactProof)>.None;
        return true;
    }
}
