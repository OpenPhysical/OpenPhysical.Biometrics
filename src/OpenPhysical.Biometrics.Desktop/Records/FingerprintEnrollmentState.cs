// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed record FingerprintEnrollmentState(
    EnrollmentSession Session,
    FingerChoice Primary,
    FingerChoice Secondary,
    FingerChoice Active,
    bool TwoFingers,
    string Feedback,
    Maybe<CapturedFingerprint> Discarded
)
{
    internal static FingerprintEnrollmentState Create(EnrollmentFingerSuggestions suggestions)
    {
        var (session, primary, secondary) = EnrollmentPresentation.Suggest(suggestions);
        return new(
            session,
            primary,
            secondary,
            primary,
            true,
            string.Empty,
            Maybe<CapturedFingerprint>.None
        );
    }

    internal FingerprintEnrollmentState Replan(
        FingerChoice primary,
        FingerChoice secondary,
        bool pair
    )
    {
        var plan = pair ? new[] { primary.Position, secondary.Position } : [primary.Position];
        var updated = Session.Replan([.. plan]);
        return this with
        {
            Primary = primary,
            Secondary = secondary,
            TwoFingers = pair,
            Session = updated.IsSuccess ? updated.Value : Session,
            Active = plan.Contains(Active.Position) ? Active : primary,
            Feedback = updated.IsSuccess ? string.Empty : updated.Error.Message,
        };
    }

    internal Result<FingerprintEnrollmentState, DesktopError> Accept(CapturedFingerprint capture) =>
        Session
            .Accept(capture, Active.Position)
            .Map(accepted =>
                this with
                {
                    Session = accepted,
                    Active = accepted.NextFinger.Match(FingerChoice.For, () => Active),
                    Feedback = string.Empty,
                    Discarded = Maybe<CapturedFingerprint>.None,
                }
            );

    internal bool ActiveAccepted =>
        Session.Accepted.Any(capture => capture.Finger == Active.Position);

    internal Maybe<CapturedFingerprint> AcceptedCapture(FingerChoice finger) =>
        Session
            .Accepted.Where(capture => capture.Finger == finger.Position)
            .Select(Maybe.From)
            .FirstOrDefault();

    internal bool CanAccept(Maybe<CapturedFingerprint> current) =>
        Feedback.Length == 0
        && !ActiveAccepted
        && EligibleCapture(current)
            .Match(capture => capture.Finger == Active.Position, () => false);

    internal bool CanAdjust(Maybe<CapturedFingerprint> current) =>
        !ActiveAccepted
        && EligibleCapture(current)
            .Match(
                value => value.Finger == Active.Position && value.Processing.HasValue,
                () => false
            );

    internal Maybe<CapturedFingerprint> EligibleCapture(Maybe<CapturedFingerprint> current) =>
        current.HasValue && Discarded.HasValue && ReferenceEquals(current.Value, Discarded.Value)
            ? Maybe<CapturedFingerprint>.None
            : current;

    internal FingerprintEnrollmentState Retry(Maybe<CapturedFingerprint> current) =>
        this with
        {
            Session = Session.Retry(Active.Position).Value,
            Discarded = current,
        };

    internal Result<ImmutableArray<CapturedFingerprint>, DesktopError> Selection(bool editing) =>
        editing
            ? new DesktopError(
                "Session.Editing",
                "Apply or cancel the capture adjustments before saving."
            )
            : Session.Selection();
}
