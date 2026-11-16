// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Recorded decision metadata; restoring this record cannot authorize a live capture.</summary>
/// <param name="Decision">Recorded bounded admission decision.</param>
/// <param name="Identity">Original frame and contour identity.</param>
/// <param name="Policy">All original qualification choices.</param>
/// <param name="ReferenceDigest">Compatible empty-reference SHA-256 when compared.</param>
/// <param name="ReferenceKeyDigest">Exact compatible empty-reference key when compared.</param>
/// <param name="QualifiedAnchorDigest">Identity of the anchored previously whole-qualified observation.</param>
/// <param name="Metrics">Measured selected-contact C spatial decision counts.</param>
/// <param name="RidgeEvidence">Source-bound full weak-component exterior witness.</param>
/// <param name="ObservedMetrics">Independent full weak-component W spatial decision counts.</param>
public sealed record Fs50WholeContactRecord(
    Fs50WholeContactDecision Decision,
    Fs50WholeContactIdentity Identity,
    Fs50WholeContactPolicy Policy,
    Maybe<string> ReferenceDigest,
    Maybe<string> ReferenceKeyDigest,
    Maybe<string> QualifiedAnchorDigest,
    Fs50WholeContactMetrics Metrics,
    Fs50WholeContactRidgeEvidence RidgeEvidence,
    Fs50WholeContactMetrics ObservedMetrics
)
{
    /// <summary>Gets the current recorded qualification implementation.</summary>
    public string Version => Fs50WholeContactPolicy.Version;

    /// <summary>Gets the complete original qualification policy SHA-256.</summary>
    public string PolicyDigest => Policy.Digest;

    /// <summary>Gets whether original image evidence admitted capture; the decision preserves background availability.</summary>
    public bool Qualified =>
        Decision
            is Fs50WholeContactDecision.QualifiedReference
                or Fs50WholeContactDecision.QualifiedGeometry
                or Fs50WholeContactDecision.SpatialCapture;

    /// <summary>Gets whether original spatial evidence permits bounded acquisition correction.</summary>
    public bool ExposureReady =>
        Qualified
        || Decision
            is Fs50WholeContactDecision.ExposureReference
                or Fs50WholeContactDecision.ExposureGeometry
                or Fs50WholeContactDecision.SpatialExposure;

    /// <summary>Validates recorded identity and geometry without granting live authorization.</summary>
    /// <param name="contour">Saved source-bound original contour.</param>
    /// <returns>Metadata consistency or a precise malformed-record failure.</returns>
    public UnitResult<CaptureError> Validate(Fs50ContactContourArtifact contour) =>
        Fs50WholeContactRecordValidation.Validate(this, contour);

    /// <summary>Validates a recorded full-W plane without creating a live capability.</summary>
    /// <param name="contour">Original source-bound feature contour.</param>
    /// <param name="observedContact">Persisted full selected weak component W.</param>
    /// <returns>Complete recorded witness association or a malformed-record failure.</returns>
    public UnitResult<CaptureError> Validate(
        Fs50ContactContourArtifact contour,
        ReadOnlySpan<byte> observedContact
    )
    {
        var valid = Validate(contour);
        if (valid.IsFailure)
            return valid;
        if (
            observedContact.Length != 800 * 750
            || Fs50PolicyDigest.Pixels(observedContact) != RidgeEvidence.ObservedContactDigest
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidFrame,
                "Recorded weak-component witness differs."
            );
        var area = 0;
        for (var i = 0; i < observedContact.Length; i++)
        {
            if (observedContact[i] > 1 || contour.ContactMask[i] > observedContact[i])
                return new CaptureError(
                    CaptureErrorCodes.InvalidFrame,
                    "Feature contact is outside its weak-component witness."
                );
            area += observedContact[i];
        }
        return area == RidgeEvidence.ObservedArea
            ? UnitResult.Success<CaptureError>()
            : new CaptureError(
                CaptureErrorCodes.InvalidFrame,
                "Recorded weak-component area differs."
            );
    }

    /// <summary>Checks original saved source provenance without reconstructing pixels or live authorization.</summary>
    /// <param name="source">Validated original acquisition metadata.</param>
    /// <param name="driverVersion">Original acquisition driver.</param>
    /// <param name="geometry">Original corrected geometry.</param>
    /// <returns>Source association or precise malformed-record failure.</returns>
    public UnitResult<CaptureError> ValidateSource(
        FingerprintProcessingSource source,
        string driverVersion,
        CaptureGeometry geometry
    ) => Fs50WholeContactRecordValidation.Source(this, source, driverVersion, geometry);
}
