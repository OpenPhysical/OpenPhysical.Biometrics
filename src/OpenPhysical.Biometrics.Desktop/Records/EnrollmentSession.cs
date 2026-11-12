// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>An immutable required-finger plan and its explicitly accepted captures.</summary>
public sealed record EnrollmentSession
{
    private EnrollmentSession(
        ImmutableArray<FingerPosition> plan,
        ImmutableArray<CapturedFingerprint> accepted
    )
    {
        Plan = plan;
        Accepted = accepted;
    }

    /// <summary>Gets required fingers in export order.</summary>
    public ImmutableArray<FingerPosition> Plan { get; }

    /// <summary>Gets accepted captures without changing their original labels.</summary>
    public ImmutableArray<CapturedFingerprint> Accepted { get; }

    /// <summary>Gets whether every planned finger is accepted.</summary>
    public bool Complete => Accepted.Length == Plan.Length;

    /// <summary>Gets the first unaccepted required finger.</summary>
    public Maybe<FingerPosition> NextFinger =>
        Plan.Where(finger => !Accepted.Any(capture => capture.Finger == finger))
            .Select(Maybe.From)
            .FirstOrDefault();

    /// <summary>Describes whether a finger is pending capture, review or acceptance.</summary>
    /// <param name="finger">Finger whose state is requested.</param>
    /// <param name="current">Current unaccepted capture.</param>
    /// <returns>The operator-visible slot state.</returns>
    public EnrollmentFingerState State(FingerPosition finger, Maybe<CapturedFingerprint> current) =>
        Accepted.Any(capture => capture.Finger == finger) ? EnrollmentFingerState.Accepted
        : Plan.Contains(finger) && current.HasValue && current.Value.Finger == finger
            ? EnrollmentFingerState.WaitingForAcceptance
        : EnrollmentFingerState.WaitingForCapture;

    /// <summary>Creates a plan containing one or two different valid fingers.</summary>
    /// <param name="plan">Required fingers in export order.</param>
    /// <returns>The empty session or an invalid-plan failure.</returns>
    public static Result<EnrollmentSession, DesktopError> Create(
        ImmutableArray<FingerPosition> plan
    ) =>
        !plan.IsDefaultOrEmpty
        && plan.Length <= 2
        && plan.All(Enum.IsDefined)
        && plan.Distinct().Count() == plan.Length
            ? new EnrollmentSession(plan, [])
            : new DesktopError("Session.Plan", "Choose one or two different fingers.");

    /// <summary>Changes the plan while retaining accepted captures still required.</summary>
    /// <param name="plan">Replacement required fingers.</param>
    /// <returns>The revised session or an invalid-plan failure.</returns>
    public Result<EnrollmentSession, DesktopError> Replan(ImmutableArray<FingerPosition> plan) =>
        Create(plan)
            .Map(_ => new EnrollmentSession(
                plan,
                Accepted.Where(capture => plan.Contains(capture.Finger)).ToImmutableArray()
            ));

    /// <summary>Reopens a required finger while preserving the other accepted captures.</summary>
    /// <param name="finger">Finger to retry.</param>
    /// <returns>The revised session or a finger-selection failure.</returns>
    public Result<EnrollmentSession, DesktopError> Retry(FingerPosition finger) =>
        Plan.Contains(finger)
            ? new EnrollmentSession(
                Plan,
                Accepted.Where(capture => capture.Finger != finger).ToImmutableArray()
            )
            : new DesktopError("Session.Finger", "Select a required finger before retrying it.");

    /// <summary>Accepts the next required finger.</summary>
    /// <param name="capture">Capture being accepted.</param>
    /// <returns>The revised session or a finger-selection failure.</returns>
    public Result<EnrollmentSession, DesktopError> Accept(CapturedFingerprint capture) =>
        NextFinger
            .ToResult(
                new DesktopError(
                    "Session.Complete",
                    "All planned fingerprints are already accepted."
                )
            )
            .Bind(finger => Accept(capture, finger));

    /// <summary>Accepts an explicitly selected required finger.</summary>
    /// <param name="capture">Capture being accepted.</param>
    /// <param name="selected">Required slot receiving the capture.</param>
    /// <returns>The revised session or a finger-selection failure.</returns>
    public Result<EnrollmentSession, DesktopError> Accept(
        CapturedFingerprint capture,
        FingerPosition selected
    ) =>
        Result
            .Success<EnrollmentSession, DesktopError>(this)
            .Ensure(
                session =>
                    session.Plan.Contains(selected)
                    && !session.Accepted.Any(accepted => accepted.Finger == selected)
                    && selected == capture.Finger,
                new DesktopError(
                    "Session.Finger",
                    "Capture the selected required finger before accepting it."
                )
            )
            .Map(_ => new EnrollmentSession(Plan, Accepted.Add(capture)));

    /// <summary>Returns a complete accepted set in the requested export order.</summary>
    /// <returns>Ordered captures or an incomplete-session failure.</returns>
    public Result<ImmutableArray<CapturedFingerprint>, DesktopError> Selection() =>
        Complete
            ? Plan.Select(finger => Accepted.Single(capture => capture.Finger == finger))
                .ToImmutableArray()
            : new DesktopError(
                "Session.Incomplete",
                "Capture and accept every planned finger before saving."
            );
}
