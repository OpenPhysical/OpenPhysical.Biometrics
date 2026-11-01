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
`Prepare` returns `Result<PortraitPreparationResult, FacialError>`; the successful
result contains immutable source-coordinate landmarks, named anatomical
features, prepared pixels, and the source-to-portrait transformation.
See [portrait preparation](PIV-PORTRAITS.md) for reviewed geometry and CLI workflows.

## Error handling

No public operation throws for expected failures or returns null. Operations return
`Result<T, TError>` or `UnitResult<TError>` from CSharpFunctionalExtensions, and
optional values use `Maybe<T>`. Each library has one error record with a stable
`Code` and a human-readable `Message`: `ImagingError`, `FormatError`, `PivError`,
`FingerprintError`, and `FacialError`. The codes live in `ImagingErrorCodes`,
`PivErrorCodes`, `FingerprintErrorCodes`, and `FacialErrorCodes`. Branch on the code,
never the message. `FormatError` also exposes located diagnostics through
`FormatError.Diagnostics`.

```csharp
var decoded = RgbImageDecoder.Load(path);
if (decoded.IsFailure)
    return Present(decoded.Error.Code, decoded.Error.Message);

// Compose steps with Bind and Map; the first failure short-circuits.
using var source = decoded.Value;
var bytes = source.Resize(480, 640, fillCrop: true).Bind(image => image.CopyRgbBytes());

// Maybe values: check HasValue or fall back explicitly.
var label = optionalText.GetValueOrDefault("not recorded");
```

The third-party NBIS and NFIQ engines still report native failures by throwing.
Applications convert those calls to results at one edge, as
`ExampleEdge.TryBind` does in the runnable examples.

## Extract and compare fingerprints

Supply the original one-byte grayscale samples, dimensions, and explicit
resolution. `FingerprintImage` (from the NBIS package) owns its samples and validates
its geometry when constructed. Wrap construction at your edge, or build an
`FingerprintCapture` with `FingerprintCapture.Create`, which validates the same
facts and returns a `Result`. Extraction produces an immutable template that can be
reused across comparisons.

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
var created = FingerprintMatchingPolicy.Create(policyIdentifier, minimumScore);
if (created.IsFailure)
    return Present(created.Error.Code); // InvalidPolicy or InvalidScore

var policy = created.Value;
var decision = policy.Evaluate(rawScore); // Result<FingerprintComparisonResult, FingerprintError>
if (decision.IsSuccess)
    Apply(decision.Value.Accepted, decision.Value.PolicyIdentifier);

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
if (parsed.IsFailure)
    return Present(parsed.Error.Diagnostics.Select(
        diagnostic => $"{diagnostic.Code} at byte {diagnostic.Offset}"));

var reference = parsed.Value.Record.ToMatchingTemplate(
    FingerPosition.RightIndex,
    viewNumber: 0); // Result<FingerprintTemplate, FingerprintError>
if (reference.IsFailure)
    return Present(reference.Error.Code); // for example ViewSelection or FingerUnavailable

var rawScore = engine.Match(probeImage, reference.Value);
var decision = policy.Evaluate(rawScore);
```

Canonical minutiae use top-left pixel coordinates, two-degree angle units, and
pixels-per-centimetre resolution. `ToMatchingTemplate` applies the NBIS coordinate
and direction conventions. [Fingerprint workflows](FINGERPRINT-WORKFLOWS.md) show
quality assessment, candidate ranking, primary/secondary enrollment, and
unavailable fingers.

## Inspect and validate records

A successful parse returns `ParsedRecord<T>`, holding the immutable record and an
`InspectionReport` of fields with byte offsets and lengths. A failed parse returns a
`FormatError` whose `Diagnostics` list the located problems. Field values are
`Maybe<string>` and are present only when `InspectionOptions.IncludeSensitiveValues`
is set. `BiometricInspector` adds a common presentation model
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
and returns `Result<FacialPreparationResult, FacialError>`. A failure reports bad
input or an encoder fault. A successful result carries a status, and its `Record`
is a `Maybe<FaceRecord>` that is present only for `Prepared`.

```csharp
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;

var preparer = new FacialRecordPreparer(logger); // ILogger<FacialRecordPreparer>
var prepared = preparer.Prepare(
    capturedSrgbPixels,
    capturedWidth,
    capturedHeight,
    acquisitionMetadata);

if (prepared.IsFailure)
{
    PresentFailure(prepared.Error.Code); // for example InvalidPixelBuffer
}
else if (prepared.Value.Record.TryGetValue(out var faceRecord))
{
    var facialRecordBytes = Incits385Codec.Write(faceRecord); // Result<byte[], FormatError>
    ContinueIssuance(facialRecordBytes, prepared.Value.Acquisition);
}
else
{
    PresentCaptureOutcome(prepared.Value.Status, prepared.Value.ProfileDiagnostics);
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
if (issued.IsFailure)
    return Present(issued.Error.Code); // for example SigningProviderFailed or Cancelled

var outcome = issued.Value; // BiometricSigningResult
if (outcome.Record.TryGetValue(out var completed)
    && outcome.Verification.TryGetValue(out var verification))
{
    ApplyIssuancePolicy(verification);
    var cardObjectBytes = CbeffCodec.Write(completed); // Result<byte[], FormatError>
    ContinueCardTransaction(cardObjectBytes);
}
else
{
    Present(outcome.Failure); // SignatureFailure, for example LengthDidNotConverge
}
```

A signing or verification provider that throws, fails, or is cancelled yields a failed
`PivError` with code `SigningProviderFailed`, `VerificationProviderFailed`, or
`Cancelled`. `VerifyAsync` accepts the parsed original record, expected card identity,
and the application verifier, and returns
`Result<BiometricVerificationReport, PivError>`. Its report retains signature, trust, revocation, and
identity-binding findings. [Signature integration](SIGNATURE-INTEGRATION.md)
describes the exact signed bytes, CMS attributes, and issuer policy handoff.

## Register processing and use owned images

Register `services.AddFingerprintProcessing()` and
`services.AddFacialRecordPreparation()` for the modern processing capabilities.
Enrollment also receives the application's selected `IFingerprintQualityProvider`.
Logging uses `Microsoft.Extensions.Logging` throughout.

For image transformations, dispose the owned buffer when its workflow finishes.
`RgbImageDecoder.Load` decodes files, streams, or bytes, and `RgbImage.Create` makes
a blank image. Every operation returns a result. `Clone` creates independent storage,
and `Clone` with a transform disposes the copy if the transform fails.
`CopyRgbBytes` supplies standard .NET byte samples to another component.
`RgbImage` has no indexer, mutation callback, or pixel-row processor; use
`GetPixel` and `SetPixel`, which return results.

```csharp
using OpenPhysical.Biometrics.Imaging;

var loaded = RgbImageDecoder.Load("capture.jpg");
if (loaded.IsFailure)
    return Present(loaded.Error.Code); // for example Imaging.DecodeFailed

using var original = loaded.Value;
var preview = original.Clone(image => image.Resize(240, 320, fillCrop: true));
if (preview.IsFailure)
    return Present(preview.Error.Code);

using var previewImage = preview.Value;
var previewRgb = previewImage.CopyRgbBytes(); // Result<byte[], ImagingError>
```

The [runnable examples](../examples/README.md) connect these steps using attributed
fingerprint fixtures, reusable templates, caller-supplied signature hooks, and
explicit application decisions.
