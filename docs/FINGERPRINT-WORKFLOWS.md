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
var capture = new FingerprintCapture(
    grayscalePixels,
    width,
    height,
    dotsPerInch: 500,
    FingerPosition.RightIndex,
    acquiredAt,
    FingerprintImpression.PlainLiveScan,
    equipmentId,
    equipmentAppendixFCertified: deviceHasAppendixFCertification);

// Quality belongs to the parent image. The provider reports the original
// five-level NFIQ 1 class and retains its algorithm or model identity.
var enrollment = new PivFingerprintEnrollmentService(engine, qualityProvider);
var prepared = enrollment.Prepare(capture);
if (prepared.ReacquisitionRecommended)
{
    // Ask the operator for another capture before choosing the final attempt.
}
```

`IFingerprintQualityProvider` is the quality-engine integration point. Its
`FingerprintQualityAssessment` preserves the NFIQ 1 class and optional confidence,
and calculates the PIV encoding as `20 * (6 - NFIQ1)`.

## Select the primary and secondary fingers

Prepare each captured attempt once, then rank the prepared values. Materializing
the projection executes extraction once per capture and makes repeated selection
and inspection inexpensive.

```csharp
var candidates = captures.Select(enrollment.Prepare).ToImmutableArray();
var ranked = PivFingerprintEnrollmentService.RankCandidates(
    candidates,
    preferLeftHand: holderPrefersLeftHand);

// The operator confirms the final pair using the ranked assessments and the
// holder's ability to present each finger to the deployed readers.
var primary = ranked[0];
var secondary = ranked[1];
var record = enrollment.CreateRecord(
    primary,
    secondary,
    new PivFingerprintRecordMetadata(
        registeredProductOwner,
        registeredProductType,
        equipmentId,
        maximumOriginalWidth,
        maximumOriginalHeight,
        equipmentAppendixFCertified: deviceHasAppendixFCertification));
var fingerprintRecordBytes = Incits378Codec.Write(record);
```

The ranker keeps the best NFIQ 1 attempt for each finger, preferring more minutiae
and then the earlier acquisition when classes tie. Fingers rank by quality and
then the preferred-hand sequence: index, middle, thumb, ring, little. The
resulting record contains primary and secondary views numbered zero. Each view
contains up to 128 minutiae selected by quality with deterministic tie-breaking.
The header uses the maximum width and height of the original captures. Minutia
coordinates continue to describe each original image.

For an unavailable finger, use `FingerprintEnrollmentFinger.Unavailable` with
`TemporarilyUnusable` or `PermanentlyUnusable`. These become PIV quality codes
255 and 254 respectively, with an empty minutia collection. Supply explicit
record geometry when both fingers are unavailable.

Pass the encoded record to the PIV CBEFF preparation workflow. The application
supplies its signature service and expected identity for the completed object.

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

// The middleware combines this decision with its signature, identity, and card
// checks. It keeps the raw score in the authorized workflow result.
var accepted = result.Accepted;
```

`FingerprintRecordAdapter.ToMatchingTemplate` makes canonical records reusable
with NBIS's image/template and template/template overloads. Conversion preserves
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

Apply your own versioned policy to each raw score with `policy.Evaluate(score)`.
Reuse extracted templates through LINQ projections when making several
comparisons. A canonical selected card view supports fluent conversion with
`record.ToMatchingTemplate(FingerPosition.RightMiddle, viewNumber: 0)`.
