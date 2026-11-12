// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

internal static class EnrollmentPresentation
{
    internal static readonly ImmutableArray<string> FingerprintProperties =
    [
        nameof(FingerprintEnrollmentViewModel.TwoFingers),
        nameof(FingerprintEnrollmentViewModel.PrimaryFinger),
        nameof(FingerprintEnrollmentViewModel.SecondaryFinger),
        nameof(FingerprintEnrollmentViewModel.ActiveFinger),
        nameof(FingerprintEnrollmentViewModel.PrimaryActive),
        nameof(FingerprintEnrollmentViewModel.SecondaryActive),
        nameof(FingerprintEnrollmentViewModel.ActiveAccepted),
        nameof(FingerprintEnrollmentViewModel.PrimaryLabel),
        nameof(FingerprintEnrollmentViewModel.SecondaryLabel),
        nameof(FingerprintEnrollmentViewModel.Progress),
        nameof(FingerprintEnrollmentViewModel.Instructions),
        nameof(FingerprintEnrollmentViewModel.Complete),
        nameof(FingerprintEnrollmentViewModel.IsBusy),
        nameof(FingerprintEnrollmentViewModel.CanAdjustCapture),
    ];

    internal static (
        EnrollmentSession Session,
        FingerChoice Primary,
        FingerChoice Secondary
    ) Suggest(EnrollmentFingerSuggestions suggestions)
    {
        var session = suggestions.Next();
        return (session, FingerChoice.For(session.Plan[0]), FingerChoice.For(session.Plan[1]));
    }

    internal static string Label(
        EnrollmentSession session,
        FingerPosition finger,
        Maybe<CapturedFingerprint> current
    ) =>
        session.State(finger, current) switch
        {
            EnrollmentFingerState.Accepted => $"{FingerChoice.LabelFor(finger)} accepted",
            EnrollmentFingerState.WaitingForAcceptance => "Waiting for acceptance",
            _ => "Waiting for capture",
        };

    internal static string Instructions(
        FingerprintEnrollmentState state,
        bool personReady,
        Maybe<CapturedFingerprint> current
    ) => Instructions(state.Session, state.Feedback, personReady, current, state.Active.Position);

    internal static string Instructions(
        EnrollmentSession session,
        string feedback,
        bool personReady,
        Maybe<CapturedFingerprint> current,
        FingerPosition active
    )
    {
        if (feedback.Length > 0)
            return feedback;
        if (!personReady)
            return "Enter a name or label for this person.";
        if (session.Complete)
            return "All planned fingerprints are accepted. Save this person's template.";
        return session.State(active, current) switch
        {
            EnrollmentFingerState.Accepted =>
                $"Reviewing accepted {FingerChoice.LabelFor(active)}.",
            EnrollmentFingerState.WaitingForAcceptance =>
                $"Review {FingerChoice.LabelFor(active)}, then accept the finger.",
            _ => $"Capture {FingerChoice.LabelFor(active)}.",
        };
    }
}
