// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class CaptureSensorEvidence(TimeProvider clock)
{
    private readonly object sync = new();
    private Fs50EmptySensorLearning learning = Fs50EmptySensorLearning
        .Create(Fs50EmptySensorPolicy.Default, clock)
        .Value;
    private Fs50WholeContactQualification qualification = Fs50WholeContactQualification
        .Create(Fs50WholeContactPolicy.Default, clock)
        .Value;
    private CapturePolicySettings policy = CapturePolicySettings.Default;
    internal CapturePolicySettings Policy
    {
        get
        {
            lock (sync)
                return policy;
        }
    }

    internal UnitResult<DesktopError> Configure(CapturePolicySettings next)
    {
        lock (sync)
        {
            var qualificationChanged = policy.WholeContact.Digest != next.WholeContact.Digest;
            if (
                policy.EvidenceOptions != next.EvidenceOptions
                || policy.EmptySensorOptions != next.EmptySensorOptions
            )
            {
                Clear();
                learning.Dispose();
                learning = Fs50EmptySensorLearning.Create(next.EmptySensor, clock).Value;
            }
            if (qualificationChanged)
            {
                qualification.Dispose();
                qualification = Fs50WholeContactQualification
                    .Create(next.WholeContact, clock)
                    .Value;
            }
            policy = next;
            return UnitResult.Success<DesktopError>();
        }
    }

    private Maybe<IFingerprintCaptureSession> session;
    private string sessionId = Guid.NewGuid().ToString("N");

    internal Result<CaptureSensorObservation, DesktopError> Observe(
        CaptureFrame frame,
        IFingerprintCaptureSession currentSession
    )
    {
        lock (sync)
        {
            if (session.HasNoValue || !ReferenceEquals(session.Value, currentSession))
            {
                Clear();
                session = Maybe.From(currentSession);
            }
            return Fs50ContactAnalysis
                .Analyze(frame, policy.Evidence)
                .Bind(contact =>
                    learning
                        .Observe(frame, contact, sessionId)
                        .Bind(observation =>
                            qualification
                                .Observe(
                                    frame,
                                    contact,
                                    observation,
                                    sessionId,
                                    CancellationToken.None
                                )
                                .Map(proof => new CaptureSensorObservation(
                                    contact,
                                    contact.Present
                                        ? contact.SpatialComponentCount == 1 && proof.Qualified
                                        : contact.Issue == Fs50ContactIssue.None
                                            && observation.Ready,
                                    proof,
                                    observation.ConfidentlyEmpty
                                ))
                        )
                )
                .MapError(error => new DesktopError(error.Code, error.Message));
        }
    }

    internal UnitResult<DesktopError> Clear()
    {
        lock (sync)
        {
            session = Maybe<IFingerprintCaptureSession>.None;
            sessionId = Guid.NewGuid().ToString("N");
            return learning
                .Clear()
                .Bind(qualification.Clear)
                .MapError(error => new DesktopError(error.Code, error.Message));
        }
    }
}
