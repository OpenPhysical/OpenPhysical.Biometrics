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
It then builds the CBEFF header and biometric content ready for signing. The
unavailable-finger example records the acquisition station's explicit status.

Registered product identifiers, equipment evidence, creator identity, credential
identity, and validity dates come from the issuer. Passing them into the example
keeps the workflow suitable for a real enrollment station.

## Sign and issue a biometric object

[SignatureIntegration.cs](OpenPhysical.Biometrics.Examples/SignatureIntegration.cs)
accepts the application's signing and verification providers. It waits for a
consistent signature-block length, checks the verification findings required by
its issuance policy, and returns bytes for the middleware's card transaction.

## Read and authenticate a card reference

[RecordReadingExample.cs](OpenPhysical.Biometrics.Examples/RecordReadingExample.cs)
parses the original CBEFF bytes, checks the biometric profile, and asks the
application's verifier to authenticate the exact issuer-signed content. The
application supplies expected card identity and evaluates the separate findings.

[Fingerprint workflows](../docs/FINGERPRINT-WORKFLOWS.md) continue from that
reference to a comparison governed by the application's versioned matching policy.

## Prepare an attended facial capture

Register `AddFacialRecordPreparation()`, supply the captured sRGB pixels and
`FacialAcquisitionMetadata`, and call `IFacialRecordPreparer.Prepare`. A prepared
result contains the canonical INCITS 385 record. Review outcomes retain the
acquisition evidence and typed findings for the capture station.

JPEG 2000 encoding uses CoreJ2K.Skia. The facial preparation tests exercise actual
encoding and record round trips; the optional facial pipeline adds capture analysis.

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
