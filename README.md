# OpenPhysical.Biometrics

OpenPhysical.Biometrics turns fingerprint captures and facial images into records
that Personal Identity Verification (PIV) applications can issue, inspect, and validate. It brings the record formats,
image quality, fingerprint extraction, and comparison workflows together behind
normal .NET types.

For a PIV issuer, the library prepares the fingerprint and facial records and the
exact content sent to a signing service. For a reader application, it parses the
stored records, explains their structure, applies the biometric profile, and
prepares the content for the application's signature verifier. Middleware combines
those findings with card identity, trust, and its authentication policy.

## Product ownership

This repository owns reusable biometric libraries and the versioned `.opbio` codec.
[OpenPhysical Enrollment Studio](https://github.com/OpenPhysical/OpenPhysical.Enrollment.Studio) owns capture, person workflows, card enrollment, validation and authorized personalization.
[OpenPhysical Issuer Studio](https://github.com/OpenPhysical/OpenPhysical.Issuer.Studio) owns manufacturing and production issuance. Both use Issuance Server for authority and card changes.

The [cross-product specification](https://github.com/OpenPhysical/OpenPhysical.Net/blob/studio-release/docs/architecture/studio-products.md) defines the current boundaries and release requirements.

## Choose your capabilities

The libraries target **.NET 10**.

- **OpenPhysical.Biometrics** provides the formats, PIV workflows, and fingerprint
  processing entry point.
- **OpenPhysical.Biometrics.Formats** provides immutable INCITS 378 fingerprint,
  INCITS 385 facial, and CBEFF records with binary readers/writers and diagnostics tied to byte offsets.
- **OpenPhysical.Biometrics.Piv** provides PIV profile checks, structured inspection,
  and integration with caller-supplied signature services.
- **OpenPhysical.Biometrics.Fingerprints** connects NIST NBIS extraction/matching and
  NFIQ 1 quality assessment to fingerprint enrollment and comparison.
- **OpenPhysical.Biometrics.Facial** prepares attended sRGB captures as JPEG 2000
  facial records and prepares Full Frontal portraits using MediaPipe landmarks.
- **OpenPhysical.Biometrics.Imaging** provides owned image buffers and Skia-backed
  decoding, encoding, and transforms.
- **OpenPhysical.Biometrics.Tool** installs `biotool` for portrait preparation, text/JSON inspection, and selected-view template conversion.
- **OpenPhysical.Biometrics.Capture.Camera** provides owned native webcam frames
  behind a Result-based, cancellable camera interface.
- **OpenPhysical.Biometrics.Packages** provides the versioned enrollment-envelope codec, v1 migration input and shared private atomic file transactions.
- **OpenPhysical.Biometrics.Presentation** provides the shared theme, controls,
  encrypted package import and privacy lock, without capture or signing services.

[NIST NBIS bindings](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NBIS)
provide MINDTCT extraction and Bozorth3 comparison. The independently versioned
[NFIQ bindings](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NFIQ) provide the original five-level NFIQ 1 assessment and the NFIQ 2
unified quality score. Each algorithm package carries its native runtime assets,
source provenance, model identity, and upstream notices.

## Desktop application

Capture, enrollment and operator instructions are maintained in the [Enrollment Studio repository](https://github.com/OpenPhysical/OpenPhysical.Enrollment.Studio). These libraries do not depend on that application.

## Read and inspect a fingerprint record

```csharp
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;

var parsed = Incits378Codec.Parse(recordBytes);
if (parsed.IsFailure)
{
    // FormatError carries a stable Code and located diagnostics.
    foreach (var diagnostic in parsed.Error.Diagnostics)
        Present(diagnostic.Code, diagnostic.Offset, diagnostic.Length);
}
else
{
    var record = parsed.Value.Record;
    var fingerViews = record.Views
        .Select(view => (view.FingerPosition, view.ViewNumber, view.Minutiae.Length))
        .ToArray();

    var profileFindings = PivBiometricProfile.Validate(record);
}

var inspection = BiometricInspector.Inspect(recordBytes, DateTimeOffset.UtcNow);
```

Public operations return `Result<T, TError>` or `UnitResult<TError>` instead of
throwing, and optional values use `Maybe<T>`. Each error type (`FormatError`,
`PivError`, `FingerprintError`, `FacialError`, `ImagingError`) carries a stable
`Code` string from its matching `*ErrorCodes` class. A successful parse returns a
`ParsedRecord<T>` with the immutable record and a structured report; a failed parse
exposes the same located diagnostics on `FormatError.Diagnostics`. Diagnostics identify
fields by stable codes and byte offsets. The same report supplies middleware and
CLI presentations. Set `InspectionOptions.IncludeSensitiveValues` explicitly when
an inspection needs field contents.

```sh
biotool inspect fingerprint.fmr --json
biotool convert fingerprint.fmr reference.json --finger 2 --view 0
biotool validate biometric.cbeff --at 2026-10-31T12:00:00.0000000+00:00
```

## Register fingerprint processing

```csharp
using OpenPhysical.Biometrics.Fingerprints;

services
    .AddFingerprintProcessing()
    .AddNfiq1FingerprintQuality();
```

Captures carry their finger identity, pixels, dimensions, nominal resolution,
acquisition time, and equipment metadata. Enrollment assesses and extracts a capture
once, then reuses the resulting template during selection. The PIV writer encodes
explicitly selected primary and secondary fingers, including unavailable-finger states.

[Fingerprint workflows](docs/FINGERPRINT-WORKFLOWS.md) explain image/template
comparisons, enrollment, quality mapping, and application-selected matching policy.

## Compare with a selected card finger

After the application authenticates the original card record, use its canonical
model directly:

```csharp
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

var decision = Incits378Codec
    .Parse(verifiedFmrBytes)
    .MapError(error => new FingerprintError(error.Code, error.Message))
    .Bind(parsed => parsed.Record.ToMatchingTemplate(FingerPosition.RightIndex, viewNumber: 0))
    .Map(reference => engine.Match(capture, reference))
    .Bind(applicationPolicy.Evaluate);

if (decision.IsFailure)
    Present(decision.Error.Code);
else
    Apply(decision.Value.Accepted, decision.Value.PolicyIdentifier);
```

Record collections support LINQ queries and immutable projections. Use `with` to
produce a changed record while keeping the original available for comparison or
re-encoding. [The workflow guide](docs/FINGERPRINT-WORKFLOWS.md) shows explicit
finger/view selection, enrollment, and paired/different-finger comparisons.

## Library integration with an issuer signing service

This is a caller-supplied library integration, not a signing feature in Biometric Studio. Issuer services own private credential keys, certificate authorization and card writes.

Implement `IBiometricSignatureProvider` and `IBiometricSignatureVerifier` in the
application's cryptographic integration. The request contains the exact CBEFF
header and biometric bytes, expected credential identity, and PIV-specific content
and signed-attribute requirements.

```csharp
var signed = await BiometricSignatureWorkflow.SignAsync(
    preparedRecord, expectedIdentity, signingService, verificationService,
    cancellationToken);

if (signed.IsFailure)
{
    // PivError.Code is a PivErrorCodes value, such as SigningProviderFailed,
    // VerificationProviderFailed, or Cancelled.
    Present(signed.Error.Code);
}
else if (signed.Value.Record.TryGetValue(out var completedRecord))
{
    var objectBytes = CbeffCodec.Write(completedRecord); // Result<byte[], FormatError>
}
```

A provider that fails or is cancelled surfaces as a failed `PivError` result rather than
an exception. The workflow resolves the signature-block length before completing the object and
requests verification over those same bytes. The returned report keeps signature,
envelope, trust, revocation, and identity-binding findings distinct. The caller
applies its authentication policy.

See [signature integration](docs/SIGNATURE-INTEGRATION.md) and the runnable
[workflow examples](examples/README.md) for ownership, provider integration, and template reuse.

## Prepare a facial record

Register `AddFacialRecordPreparation()` from the optional facial package. Supply
captured, tightly packed sRGB pixels and explicit camera/operator acquisition
findings. The preparer encodes the original geometry as JPEG 2000 and returns an
INCITS 385 record with typed profile findings. `Prepare` returns
`Result<FacialPreparationResult, FacialError>`, and the record is a `Maybe<FaceRecord>`
that is present only when the status is `Prepared`. The preparer requires an
`ILogger<FacialRecordPreparer>`. JPEG 2000 encoding uses
**CoreJ2K.Skia**, and the shared imaging package uses SkiaSharp for image I/O and
transforms.

## Build and develop

This repository restores the pinned NBIS and NFIQ NuGet packages from the
configured package feed. CI restores locked package versions in an isolated
clone. Test projects restore the private, immutable
`OpenPhysical.Biometrics.TestFixtures` package from GitHub Packages. Its source
snapshots and hashes remain in this repository. Configure package access as
described in [build and release](docs/BUILD_AND_RELEASE.md).

```sh
dotnet tool restore
python3 scripts/verify-test-fixture-package.py
dotnet restore OpenPhysical.Biometrics.sln --locked-mode
dotnet build OpenPhysical.Biometrics.sln -c Release -m:1
dotnet test OpenPhysical.Biometrics.sln -c Release --no-build --no-restore -m:1
```

Run .NET builds and tests serially. CSharpier, public XML documentation, analyzers,
and warnings-as-errors are build gates. Native algorithm tests live with their
bindings; NUnit covers the managed workflows and integrations.

Release packages use the pinned NBIS and NFIQ package versions in
`Directory.Build.props`. Package validation is documented in
[`docs/RELEASING.md`](docs/RELEASING.md).

The [validation record](docs/VALIDATION_SUMMARY.md) lists exercised workflows and
reproduction commands. The [architecture guide](docs/ARCHITECTURE.md) explains package ownership, the
current capture/issuance boundary and the historical package-extraction sequence.

The [build and release guide](docs/BUILD_AND_RELEASE.md) documents package feed
access, isolated CI and fixture provenance.

## Licensing and contributions

First-party code is available under GPL v2 or later and separately negotiated
commercial terms. Dependency notices preserve their applicable licenses. See
[commercial licensing](COMMERCIAL-LICENSING.md), [contribution policy](CONTRIBUTING.md),
[Bounded Contribution Policy](CODE_OF_CONDUCT.md), and [security reporting](SECURITY.md).

## Prepare a PIV portrait

Use `biotool face prepare capture.jpg --output portrait.jp2 --preview portrait.png` to prepare a 480 × 640 facial image. MediaPipe locates the facial features, and the library selects a Full Frontal crop from the source pixels. Reviewed head boundaries and acquisition metadata support creation of the INCITS 385 record. See [the portrait workflow](docs/PIV-PORTRAITS.md) for examples and geometry review.
