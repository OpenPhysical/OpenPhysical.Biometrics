# Preparing fingerprints for PIV

`OpenPhysical.Biometrics.Fingerprints` connects the canonical INCITS 378 record
model to NIST NBIS extraction and matching. Captures carry their finger identity,
acquisition instant, image geometry, and equipment identifier. Templates and
captures own immutable data, so the same prepared capture can be reused safely.

## Prepare captures once

Use a registered equipment identifier and the actual acquisition metadata. A raw
image contains one byte per pixel, with black at zero and white at 255. Supply
the original dimensions and the explicit 500 ppi resolution.

```csharp
var created = FingerprintCapture.Create(
    grayscalePixels,
    width,
    height,
    dotsPerInch: 500,
    FingerPosition.RightIndex,
    acquiredAt,
    FingerprintImpression.PlainLiveScan,
    equipmentId,
    equipmentAppendixFCertified: deviceHasAppendixFCertification);
if (created.IsFailure)
    return Present(created.Error.Code); // for example InvalidCapture or InvalidEquipment

// Quality belongs to the parent image. The provider reports the original
// five-level NFIQ 1 class and retains its algorithm or model identity.
var processing = new FingerprintPreparationService(engine, qualityProvider);
var enrollment = new PivFingerprintEnrollmentService(processing);
var prepared = enrollment.Prepare(created.Value);
if (prepared.IsFailure)
    return Present(prepared.Error.Code); // for example QualityEngineFailed or EngineFailed

if (prepared.Value.ReacquisitionRecommended)
{
    // Ask the operator for another capture before choosing the final attempt.
}
```

`FingerprintCapture.Create` copies the pixels, so the caller may clear its buffer
afterward. Every failure is a `FingerprintError`; branch on its `Code` from
`FingerprintErrorCodes`.

`IFingerprintQualityProvider` is the quality-engine integration point. Its
`FingerprintQualityAssessment` preserves the NFIQ 1 class and optional confidence,
and calculates the PIV encoding as `20 * (6 - NFIQ1)`.

## Prepare an unsigned capture

`FingerprintPreparationService.Prepare(FingerprintImage)` assesses and extracts an immutable
nominal 500 ppi image without requiring issuance-equipment metadata. Its `PreparedFingerprint`
retains the exact image, full NBIS extraction and NFIQ 1 assessment. Reuse that result when
reviewing or exporting the capture.

For bounded candidate selection, call `Assess(image)` to obtain an immutable
`AssessedFingerprint` without extracting minutiae. Call `Extract(assessed)` only
for the selected image; it reuses that image's NFIQ 1 result. The existing
`Prepare(image)` composes these same operations in the original order and leaves
PIV output unchanged. Supplemental NFIQ 2 assessment remains an independently
selected provider.

`FingerprintViewFactory.Create` converts the extraction into a selected finger/view with the
same deterministic 128-point selection and coordinate conversion used by PIV issuance. Assemble
one or two resulting views in the foundation `FingerprintRecord` and encode with `Incits378Codec`.
Render the canonical views over the original image so the displayed points are exactly those
written to the unsigned FMR file.

For repeated comparisons, convert each final record view once with `ToMatchingTemplate` and use
the template overload of `FingerprintComparisonService.Compare`, supplying the explicit finger
identities and matching policy. This compares the retained canonical subset, including its
two-degree angle quantization, without extracting either source image again.

## Add supplemental NFIQ 2 quality

Reference the opt-in `OpenPhysical.Biometrics.Fingerprints.Nfiq2` project/package and
call `services.AddNfiq2FingerprintQuality()`. The container owns one reusable native
engine and model, with calls serialized by the NFIQ 2 wrapper. Existing core
Fingerprint/PIV consumers do not acquire the NFIQ 2 or OpenCV dependencies.

`IFingerprintUnifiedQualityProvider.Assess(prepared.Image)` assesses the exact prepared
500 ppi grayscale image once. It returns `FingerprintUnifiedQualityAssessment.Score`
and `AlgorithmVersion`, or a typed error. `QualityImageNotAssessable` means NFIQ 2
could not assess the usable fingerprint area; `QualityEngineFailed` identifies an
operational failure. Do not replace a failed fresh assessment with an absent score.

NFIQ 2 reports a continuous score from **0 through 100**, with higher scores indicating
greater expected recognition utility. It does not define standardized adjective bands
for this display. See [NIST's NFIQ 2 description](https://www.nist.gov/services-resources/software/nfiq-2)
and [NISTIR 8382](https://nvlpubs.nist.gov/nistpubs/ir/2021/NIST.IR.8382.pdf).
Keep NFIQ 1's five-level class and PIV encoding unchanged; the supplemental score
belongs in application metadata, not the PIV quality field.

The NFIQ 2 runtime package includes Apple Silicon assets. Distributors must retain
its notices, license texts, and corresponding-source/replacement instructions for
the dynamically linked FingerJet library; see the runtime package's
`THIRD_PARTY_NOTICES.md` and `NFIQ2-PACKAGE-LICENSE.md`.

## Select the primary and secondary fingers

Prepare each captured attempt once, then rank the prepared values. Materializing
the projection executes extraction once per capture and makes repeated selection
and inspection inexpensive.

```csharp
var candidates = captures.Select(enrollment.Prepare).ToImmutableArray();
if (candidates.Any(candidate => candidate.IsFailure))
    return Present("A capture could not be prepared.");

var ranked = PivFingerprintEnrollmentService.RankCandidates(
    candidates.Select(candidate => candidate.Value),
    preferLeftHand: holderPrefersLeftHand);
if (ranked.IsFailure)
    return Present(ranked.Error.Code); // UnavailableCandidate

// The operator confirms the final pair using the ranked assessments and the
// holder's ability to present each finger to the deployed readers.
var primary = ranked.Value[0];
var secondary = ranked.Value[1];
var record = PivFingerprintRecordMetadata
    .Create(
        registeredProductOwner,
        registeredProductType,
        equipmentId,
        maximumOriginalWidth,
        maximumOriginalHeight,
        equipmentAppendixFCertified: deviceHasAppendixFCertification)
    .Bind(metadata => enrollment.CreateRecord(primary, secondary, metadata));
if (record.IsFailure)
    return Present(record.Error.Code); // for example DuplicateFingers or RecordDimensions

// Writing is a format operation, so its failure is a FormatError.
var fingerprintRecordBytes = Incits378Codec.Write(record.Value);
if (fingerprintRecordBytes.IsFailure)
    return Present(fingerprintRecordBytes.Error.Code);
```

The ranker keeps the best NFIQ 1 attempt for each finger, preferring more minutiae
and then the earlier acquisition when classes tie. Fingers rank by quality and
then the preferred-hand sequence: index, middle, thumb, ring, little. The
resulting record contains primary and secondary views numbered zero. Each view
contains up to 128 minutiae selected by quality with deterministic tie-breaking.
The header uses the maximum width and height of the original captures. Minutia
coordinates continue to describe each original image.

Unavailable-finger encoding records an actual acquisition/unavailability finding. It is not the planned issuer-side `NotEnrolled` state for a synthetic unpersonalized test card, and missing capture data must not silently select it. That future profile is specified in the [hosted test issuance plan](../../OpenPhysical.Net/docs/planning/hosted-test-issuance.md).

For an unavailable finger, use `FingerprintEnrollmentFinger.Unavailable` with
`TemporarilyUnusable` or `PermanentlyUnusable`; it returns a
`Result<FingerprintEnrollmentFinger, FingerprintError>`. For such a finger the
`Capture`, `Template`, and `Quality` properties are `Maybe` values with no value. These become PIV quality codes
255 and 254 respectively, with an empty minutia collection. Supply explicit
record geometry when both fingers are unavailable.

Pass the encoded record to the PIV CBEFF preparation workflow. The application
supplies its signature service and expected identity for the completed object.

## Select minutiae for an off-card PIV template

SP 800-76-2 section 4.4.2, Table 6 row 31 allows zero through 128 minutiae per
finger and advises another enrollment attempt when at most ten are found. It
sets no lower target count or mandatory per-point quality cutoff. A result with
63 points therefore does not need shortening solely because of its count.
The separate on-card comparison conversion rules and reported count percentiles
in section 5.6 do not define a limit for this off-card record.

The shared view factory ranks the existing extraction by local minutia quality,
removes exact coordinate duplicates, and retains at most 128 points. Studio first
applies its explicit feature-eligibility mask. It does not apply a proximity grid
or discard points merely because their display color is orange. NFIQ 1 and NFIQ 2
assess the image; their scores are not per-point pruning thresholds.

Additional pruning is an application-policy experiment, not a formatting fix.
Validate a fixed candidate against genuine and non-mated comparisons before
adoption; fewer points or a higher image-quality score alone does not establish
better matching. Keep exported, displayed and compared canonical points identical.

Source: [NIST SP 800-76-2](https://nvlpubs.nist.gov/nistpubs/specialpublications/nist.sp.800-76-2.pdf),
sections 4.4.2 and 5.6.

## Compare a fresh capture with the authenticated record

Select the reference finger and view explicitly. Keep the application's acceptance
policy version with the comparison result so a later decision can be interpreted
using the same settings.

```csharp
var comparison = new FingerprintComparisonService(engine);
var result = comparison.Compare(
    freshCapture,
    authenticatedRecord,
    FingerPosition.RightIndex,
    viewNumber: 0,
    applicationMatchingPolicy);
if (result.IsFailure)
    return Present(result.Error.Code); // for example FingerMismatch or ViewSelection

// The middleware combines this decision with its signature, identity, and card
// checks. It keeps the raw score in the authorized workflow result.
var accepted = result.Value.Accepted;
```

`FingerprintRecordAdapter.ToMatchingTemplate` returns a
`Result<FingerprintTemplate, FingerprintError>` that makes canonical records reusable
with NBIS's image/template and template/template overloads. It fails with
`ViewSelection`, `FingerUnavailable`, or `UnsupportedResolution` when the selection
or record cannot support matching. Conversion preserves
the 197 pixels/cm nominal 500 ppi scale, reverses the ridge direction by 180
degrees, and maps top-origin image rows to NBIS's height-minus-row coordinates.

Register processing with `services.AddFingerprintProcessing()` and register an
`IFingerprintQualityProvider` for enrollment. NBIS serializes native operations
across engine instances. LINQ projections are useful for reusable templates;
ordinary loops keep operator interactions and external side effects explicit.

## Compare paired impressions and different fingers

Run the named SD04 assortment through all four capture/template combinations:

```text
dotnet run --project examples/OpenPhysical.Biometrics.Examples -- --sd04 ../OpenPhysical.Biometrics.NBIS/tests/fixtures/sd04
```

The example selects paired impressions `f0005_03` and `s0005_03`, then compares
`f0070_10` and `f0003_10` against that reference. Its explicit
`sd04-assortment-regression-v1` policy uses score 40 for these three selected comparisons.
The paired impressions are accepted and the different-finger comparisons are
rejected. Every decision retains the policy identifier.

Create your own versioned policy with `FingerprintMatchingPolicy.Create(identifier,
minimumScore)` and apply it to each raw score with `policy.Evaluate(score)`; both
return results. Reuse extracted templates through LINQ projections when making several
comparisons. A canonical selected card view converts with
`record.ToMatchingTemplate(FingerPosition.RightMiddle, viewNumber: 0)`, and the
resulting `Result` composes with `Bind` and `Map`.

## Qualified acquisition handoff

`AcquiredFingerprintAdapter` maps a corrected plain `CaptureFrame` into the existing `FingerprintCapture` processing path. The caller supplies an externally approved `FingerprintAcquisitionProfile` with exact device identity, driver/correction versions, calibration digest, output geometry, qualification reference and registered equipment mapping. Constructing this profile does not authenticate policy or certify hardware; the Server/Workstation authority owns those decisions.

This adapter requires measured corrected geometry at 500 ppi on both axes, matching the existing processing input. It preserves the original corrected pixels and capture time. Raw, rolled, experimental, unknown-resolution and unknown-equipment input fail with typed errors. This does not qualify the experimental FS50 geometry. Private experimental-record and prepared-evidence submission workflows remain separate work; no CA or operator authorization is added to Biometrics.

### Prepared acquisition lineage

`AcquiredFingerprintPreparationService` composes the qualified acquisition adapter with the existing quality/extraction and PIV record services. `Prepare` returns a reusable `PreparedAcquiredFingerprint`; selecting and encoding two prepared fingers does not repeat extraction. `CreateRecord` returns immutable unsigned INCITS 378 bytes and ordered `FingerprintPreparationEvidence` for each view.

Each captured view retains the approved acquisition mapping, original calibration/driver/geometry provenance, capture time and sequence, corrected Gray8 pixel SHA-256, actual quality assessment and extraction description. Every evidence entry binds to the SHA-256 of the complete encoded record. `ProcessingVersion` identifies the selection/conversion algorithm independently of provider provenance. The returned record contains no raw pixels; callers control the lifetime of their local prepared selections. Evidence is sensitive metadata and is not a device attestation, operator authorization or CA signature. Server policy verifies the submitted workflow context separately before signing.

`PreparedAcquiredFingerprint.Unavailable` preserves temporary and permanent unavailability with no capture, calibration, pixel hash or quality claims. It cannot express an available finger without processing a capture. Omission remains a separate issuance policy choice, not an unavailable-finger marker. The existing PIV equipment, dimension, finger-order and certification requirements still apply.

## Scanner removal regression

An isolated run of baseline `6075eb73583e31915e84710f4ceb34a5932fa113` reproduced
`CaptureAutomationTests.RejectedContactCannotSatisfyTheRemovalRequiredForAnotherCapture`:
repeated pale contact could release the removal gate and recapture the same finger.
The fix also compares the coherent spatial footprint, which remains available before
darkness rejection, with the last qualified footprint. It uses the existing residual
fraction, exact frame/session association, two-frame requirement and maximum gap.
Fingerprint quality and matching policies are unchanged.

Existing public-image regressions cover repeated weak contact, empty-frame removal,
manual and automatic capture, and faint incoherent residue that permits a new finger
without becoming an accepted capture or an empty-sensor reference. These deterministic
checks do not qualify scanner hardware. Before deploying this change to a physical
scanner, check lightened contact without lifting, actual removal with faint residue,
and the next finger at the configured exposure in both capture modes. Confirm that
only an actual new presentation can rearm, while cancellation still drains acquisition.
