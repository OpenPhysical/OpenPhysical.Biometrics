# .NET API guide

OpenPhysical.Biometrics prepares and interprets the biometric records used by PIV
credentials. A typical issuance workflow captures a fingerprint or portrait,
prepares its interchange record, checks the PIV profile, and asks the application's
signature service to authenticate the completed CBEFF object. A reader parses the
stored object, authenticates it, and compares a fresh capture with the selected
reference finger.

All current packages target .NET 10. Public XML documentation describes ownership,
units, expected outcomes, and validation at each entry point.

## Choose the capabilities your application uses

`OpenPhysical.Biometrics` brings together canonical formats, PIV workflows, and
fingerprint processing. `.Formats` supplies immutable INCITS 378 fingerprint,
INCITS 385 facial, and CBEFF records. `.Piv` supplies profile validation,
inspection, and signature handoffs. `.Facial` prepares captured sRGB pixels as a
JPEG 2000 facial record. `.Imaging` supplies an owned `RgbImage` buffer for image
loading, transforms, and encoding.

`PortraitPreparer` in `.Facial` uses the pinned `.Facial.Models` assets through
Microsoft.ML.OnnxRuntime. Register `AddPortraitPreparation()` for lazy CPU inference.
The result contains immutable source-coordinate landmarks, named anatomical
features, prepared pixels, and the source-to-portrait transformation.
See [portrait preparation](PIV-PORTRAITS.md) for reviewed geometry and CLI workflows.

## Extract and compare fingerprints

Supply the original one-byte grayscale samples, dimensions, and explicit
resolution. `FingerprintImage` owns its samples, and extraction produces an
immutable template that can be reused across comparisons.

```csharp
using System.Collections.Immutable;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

var engine = new NbisFingerprintEngine();
var referenceImage = new FingerprintImage(referencePixels, width, height, 500);
var probeImage = new FingerprintImage(probePixels, width, height, 500);
var reference = engine.Extract(referenceImage);
var probe = engine.Extract(probeImage);
var rawScore = engine.Match(probe, reference);

// Record the application's policy identifier with its decision.
var policy = new FingerprintMatchingPolicy(policyIdentifier, minimumScore);
var decision = policy.Evaluate(rawScore);

// Materialize extraction once when the same captures support several comparisons.
var templates = capturedImages.Select(engine.Extract).ToImmutableArray();
var decisions = templates.Select(template =>
    policy.Evaluate(engine.Match(template, reference))).ToImmutableArray();
```

NBIS supports image/image, image/template, template/image, and template/template
comparisons. Its raw Bozorth3 score feeds the application's matching policy.
The native engine serializes its operations across instances.

## Select a reference from a card record

Parse the raw INCITS 378 record after the caller unwraps the card data object.
Select the finger and view explicitly when creating an NBIS matching template.

```csharp
using OpenPhysical.Biometrics.Formats.Fingerprints;

var parsed = Incits378Codec.Parse(rawFingerprintRecord);
if (!parsed.IsSuccess)
    throw new FormatException(string.Join("; ", parsed.Diagnostics.Select(
        diagnostic => $"{diagnostic.Code} at byte {diagnostic.Offset}")));

var reference = parsed.Value!.ToMatchingTemplate(
    FingerPosition.RightIndex,
    viewNumber: 0);
var rawScore = engine.Match(probeImage, reference);
var decision = policy.Evaluate(rawScore);
```

Canonical minutiae use top-left pixel coordinates, two-degree angle units, and
pixels-per-centimetre resolution. `ToMatchingTemplate` applies the NBIS coordinate
and direction conventions. [Fingerprint workflows](FINGERPRINT-WORKFLOWS.md) show
quality assessment, candidate ranking, primary/secondary enrollment, and
unavailable fingers.

## Inspect and validate records

A parse result contains the immutable value, typed diagnostics, and fields with
byte offsets and lengths. `BiometricInspector` adds a common presentation model
and PIV profile findings for raw FMR, FAC, or CBEFF data.

```csharp
using OpenPhysical.Biometrics.Piv;

var report = BiometricInspector.Inspect(rawObject, evaluationTime);
foreach (var finding in report.Structure.Diagnostics)
    PresentStructureFinding(finding.Code, finding.Offset, finding.Length);
foreach (var finding in report.ProfileDiagnostics)
    PresentProfileFinding(finding.Code, finding.Field);
```

Pass the evaluation instant explicitly for validity checks. Applications can opt
into field contents with `InspectionOptions(IncludeSensitiveValues: true)` for an
authorized inspection view. Keep image samples, minutiae, identities, and matching
scores in the protected workflow result; operational logs carry status and event
identifiers.

## Prepare a captured portrait

`FacialRecordPreparer` accepts tightly packed sRGB samples and acquisition facts
from the camera and attended capture workflow. It preserves the supplied geometry
and returns a typed preparation outcome and canonical facial record.

```csharp
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;

var preparer = new FacialRecordPreparer(logger);
var prepared = preparer.Prepare(
    capturedSrgbPixels,
    capturedWidth,
    capturedHeight,
    acquisitionMetadata);

if (prepared.Status == FacialPreparationStatus.Prepared)
{
    var facialRecordBytes = Incits385Codec.Write(prepared.Record!);
    ContinueIssuance(facialRecordBytes, prepared.Acquisition);
}
else
{
    PresentCaptureOutcome(prepared.Status, prepared.ProfileDiagnostics);
}
```

Acquisition metadata records source, device, time, quality, and confirmation of the
frontal subject and optical capture requirements. The shared PIV profile checks
encoded facial metadata, including width greater than 420 pixels. The portrait preparer adds detection, named landmarks, roll correction, and
[geometry findings](PIV-PORTRAITS.md).

## Connect the application's signature services

The issuer supplies `IBiometricSignatureProvider` and
`IBiometricSignatureVerifier`, along with the expected `BiometricIdentity` and a
prepared `CbeffRecord`.

```csharp
using OpenPhysical.Biometrics.Formats.Cbeff;

var issued = await BiometricSignatureWorkflow.SignAsync(
    preparedRecord, expectedIdentity, signer, verifier, cancellationToken);
if (issued.Record is { } completed)
{
    ApplyIssuancePolicy(issued.Verification!);
    var cardObjectBytes = CbeffCodec.Write(completed);
    ContinueCardTransaction(cardObjectBytes);
}
```

`VerifyAsync` accepts the parsed original record, expected card identity, and the
application verifier. Its result retains signature, trust, revocation, and
identity-binding findings. [Signature integration](SIGNATURE-INTEGRATION.md)
describes the exact signed bytes, CMS attributes, and issuer policy handoff.

## Register processing and use owned images

Register `services.AddFingerprintProcessing()` and
`services.AddFacialRecordPreparation()` for the modern processing capabilities.
Enrollment also receives the application's selected `IFingerprintQualityProvider`.
Logging uses `Microsoft.Extensions.Logging` throughout.

For image transformations, dispose the owned buffer when its workflow finishes.
`Clone` creates independent storage; `CopyRgbBytes` supplies standard .NET byte
samples to another component.

```csharp
using OpenPhysical.Biometrics.Imaging;

using var original = RgbImage.Load("capture.jpg");
using var preview = original.Clone(image => image.Resize(240, 320, fillCrop: true));
var previewRgb = preview.CopyRgbBytes();
```

The [runnable examples](../examples/README.md) connect these steps using attributed
fingerprint fixtures, reusable templates, caller-supplied signature hooks, and
explicit application decisions.
