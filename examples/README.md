# Biometric workflow examples

These examples follow the steps a capture station, PIV issuer, or reader application
performs. The comments explain where data comes from, who owns it, and which work
is worth doing once and reusing.

## Compare two captured impressions

Build the development dependencies, then run the committed SD04 pair through quality
assessment, extraction, and all four comparison combinations:

```sh
dotnet run --project examples/OpenPhysical.Biometrics.Examples -c Release -- ../OpenPhysical.Biometrics.NBIS/tests/fixtures/sd04/f0005_03.gray8 ../OpenPhysical.Biometrics.NBIS/tests/fixtures/sd04/s0005_03.gray8 512 512
```

The NBIS and NFIQ engines throw on native failures, so
[ExampleEdge.cs](OpenPhysical.Biometrics.Examples/ExampleEdge.cs) is the single place
that converts those exceptions to a failed `Result`. Every example reports failures
through results and an exit code, not exceptions.

[Program.cs](OpenPhysical.Biometrics.Examples/Program.cs) shows image/image,
image/template, template/image, and template/template comparison, NFIQ 1 and NFIQ 2
assessments, a JSON template round trip, and LINQ projections that materialize
expensive results before reuse. Inputs are top-left-origin grayscale pixels at
nominal 500 ppi. Completion output keeps the biometric results in application memory.

The images come from Craig I. Watson's NIST Special Database 4. Their original
files, source checksums, decoding receipt, and NIST data terms are retained in the
[NBIS fixture collection](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NBIS/tree/master/tests/fixtures/sd04).

## Prepare enrollment for issuance

[EnrollmentExample.cs](OpenPhysical.Biometrics.Examples/EnrollmentExample.cs) takes
captures with acquisition metadata, assesses each attempt once, ranks candidates
for review, selects two explicit fingers, and creates their fingerprint record.
It then builds the CBEFF header and biometric content ready for signing. Each step
returns a `Result`, so the first failure, such as an unpreparable capture or a record
needing profile review, becomes a failed result with a message. The unavailable-finger
example returns `FingerprintEnrollmentFinger.Unavailable`, a
`Result<FingerprintEnrollmentFinger, FingerprintError>` carrying the acquisition
station's explicit status.

Registered product identifiers, equipment evidence, creator identity, credential
identity, and validity dates come from the issuer. Passing them into the example
keeps the workflow suitable for a real enrollment station.

## Sign and issue a biometric object

[SignatureIntegration.cs](OpenPhysical.Biometrics.Examples/SignatureIntegration.cs)
accepts the application's signing and verification providers. It waits for a
consistent signature-block length, checks the verification findings required by
its issuance policy, and returns bytes for the middleware's card transaction. A failed
`PivError` result, including provider failure and cancellation, becomes a failed result;
the `Maybe` record and verification in a successful signing result are checked before
use.

## Read and authenticate a card reference

[RecordReadingExample.cs](OpenPhysical.Biometrics.Examples/RecordReadingExample.cs)
parses the original CBEFF bytes with `CbeffCodec.Parse`, checks the biometric profile,
and asks the application's verifier to authenticate the exact issuer-signed content.
The application supplies expected card identity and evaluates the separate findings.
Structural, profile, and verifier failures each return a failed result.

[Fingerprint workflows](../docs/FINGERPRINT-WORKFLOWS.md) continue from that
reference to a comparison governed by the application's versioned matching policy.

## Prepare an attended facial capture

Register `AddFacialRecordPreparation()`, supply the captured sRGB pixels and
`FacialAcquisitionMetadata`, and call `IFacialRecordPreparer.Prepare`, which returns
`Result<FacialPreparationResult, FacialError>`. A prepared result holds the canonical
INCITS 385 record in a `Maybe<FaceRecord>`. Review outcomes leave it empty and retain
the acquisition evidence and typed findings for the capture station.

JPEG 2000 encoding uses CoreJ2K.Skia. The facial preparation tests exercise actual
encoding and record round trips; `PortraitPreparer` supplies source-supported Full Frontal pixels.

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

Create your own versioned policy with `FingerprintMatchingPolicy.Create` and apply it to
each raw score with `policy.Evaluate(score)`; both return results. Reuse extracted
templates through LINQ projections when making several comparisons. A canonical
selected card view converts with
`record.ToMatchingTemplate(FingerPosition.RightMiddle, viewNumber: 0)`, which returns a
`Result<FingerprintTemplate, FingerprintError>`.

## Prepare and review a portrait

[PortraitExample.cs](OpenPhysical.Biometrics.Examples/PortraitExample.cs) prepares an
encoded capture, exposes review findings, and feeds the resulting pixels into the
canonical facial-record preparer, chaining the steps with `Ensure` and `Bind`. The caller supplies reviewed boundaries and
camera/operator evidence. The [CLI examples](../docs/PIV-PORTRAITS.md) cover
preview output, explicit face selection, reviewed geometry, and record creation.
