# Biometric processing and middleware boundaries

OpenPhysical.Biometrics owns biometric interpretation and the capture application. OpenPhysical.Net owns credential authorization, issuer signing and card administration. Applications may consume shared codecs and presentation without sharing these capabilities.

OpenPhysical Biometric Studio captures and reviews real data and exports encrypted unsigned `.opbio`. It has no credential-issuance, content-signing or card-administration service. OpenPhysical Issuance Studio is the target name for Net's current Signing application; it imports packages, issues and writes cards, and has no capture stack. The [hosted test issuance plan](../../OpenPhysical.Net/docs/planning/hosted-test-issuance.md) distinguishes planned first-card/account flows from current installed tooling.

## Current package graph

`OpenPhysical.Biometrics.Formats` owns the canonical CBEFF, INCITS 378, and INCITS
385 models and wire codecs. `.Piv` depends on Formats and adds profile validation,
inspection, and the signature-service contracts. `.Fingerprints` depends on
Formats plus the independently versioned NBIS and NFIQ1 bindings. The root package
composes those entry points.

`.Facial` depends on Formats/Piv and CoreJ2K.Skia for captured-pixel preparation.
`.Imaging` owns buffers and Skia-backed image operations. `.Onnx` carries
the pinned MediaPipe models; `.Facial` runs them through ONNX Runtime and prepares
portraits using named anatomical features and reviewed head boundaries.

The independent NBIS repository owns MINDTCT/Bozorth3 native adaptation and its
reference tests. The independent NFIQ repository owns NFIQ1/NFIQ2 native adaptation,
model provenance, and reference tests. Their packages provide explicit runtime
assets and operational logging.

## Error handling and size rules

The libraries use no exceptions and no nulls in their public APIs. Operations return
`Result<T, TError>` or `UnitResult<TError>` from CSharpFunctionalExtensions, and
optional values use `Maybe<T>`. Each library owns one error record with a stable
`Code`: `ImagingError`, `FormatError`, `PivError`, `FingerprintError`, and
`FacialError`. Codes are constants in the matching `*ErrorCodes` class, and callers
branch on them rather than on message text. Callers compose steps with `Bind` and
`Map`, so the first failure short-circuits.

Each project keeps a single edge helper where code that can throw meets the
Result-based workflow, for example `ProviderEdge` in `.Piv` for caller-supplied signing
services and `FingerprintEdge` in `.Fingerprints` for the native NBIS engine. The
helper converts exceptions and cancellation to failed results with stable codes. All
other code stays free of `try`/`catch`.

Files stay small enough to read in a slice of a model's context window. Every `.cs`
file is limited to 250 lines, every type to 200 lines, and every method to 40 lines.
Split oversized code by responsibility.
`python scripts/check-file-size.py` enforces all of these limits and runs in CI.

## Open source middleware ownership

The consolidated OpenPhysical.Net solution separates portable Core contracts, read-only Verification, CardEdge reader adapters, Cards execution/signing, Issuance workflows and the Server/desktop hosts. Historical `OpenPhysical.Middleware.*`, CardManagement and VirtualPiv namespaces may remain in moved sources; they are not separate current projects.

Verification and the capture app do not contain concrete credential private-key signing or card-write services. The capture-side Check card module may consume Verification and reader adapters to authenticate existing objects and compare a live probe. TLS operator authentication is separate from issuing credentials.

Biometric adapters consume the public Formats/Piv packages. They map canonical
inspection into middleware presentations and supply cryptographic services to
the signature hooks. Enrollment inputs carry actual fingerprint/facial records
and acquisition evidence into provisioning. Middleware combines signature,
identity, trust, revocation, biometric profile, and comparison findings according
to its application policy.

PACS and the consolidated OSDP library retain their reusable open source capabilities.
Gp4Net, Kdf108, and other independent libraries keep their own release lifecycles.

## Commercial product ownership

Pivantra's product layer owns its commercial enrollment screens, operator
workflows, fleet administration, business integrations, and deployment experience.
It consumes the same OpenPhysical public APIs as other applications. Commercial
product code depends on the open source packages in that direction.

The final allocation of existing product-specific UI and orchestration classes
belongs to the product owners. Class-by-class migration follows an inventory of
their actual dependencies and rights coverage.

## Historical package-extraction sequence

The following records the earlier extraction plan. It is not the current project graph or a requirement to move the two Studio applications into Pivantra. The consolidated Net source and hosted test issuance plan govern current ownership. No new executable or combined capture/signing app is proposed here.

1. Release the independent algorithm bindings with reproducible native assets,
   NIST reference vectors, sanitizer checks, and isolated package consumers.
2. Release canonical Biometrics 3 packages. Connect
   middleware parsing, inspection, enrollment, and signature integration to those
   contracts.
3. Establish OpenPhysical.Middleware repository/package boundaries around the
   essential credential services and emulator-backed workflows.
4. Move product-specific orchestration and UI into Pivantra in focused changes,
   consuming the released public packages and preserving transaction semantics.

Each migration keeps its release notes, explicit package versions, caller-owned
trust policy, and tests at the layer that owns the behavior. Native parity belongs
to µnit; managed ownership, conversions, services, and workflow composition belong
to NUnit; card transactions belong to middleware integration tests.

## Shared presentation and private handoff

`OpenPhysical.Biometrics.Presentation` owns themes, stage controls, file dialogs, encrypted package import and privacy locking without capture or signing services. Issuance uses that library instead of `DesktopServices.CreateHost`, which registers capture sources. `OpenPhysical.Biometrics.Packages` preserves unsigned artifacts and producer provenance; it grants no identity, allocation or signing authority.

The planned `SyntheticTestFace` branch belongs to Issuance Studio and is explicitly synthetic TEST provenance. It is not a fabricated captured person or a sensor result. Real capture requires its own accepted acquisition evidence. Credential CA keys, tenant/manufacturer attestation keys and GP management keys remain separate issuer concerns; the capture app receives none of them.

## Device acquisition boundary

`OpenPhysical.Biometrics.Capture` is a native-free leaf library. It owns immutable device capabilities, acquisition settings, Gray8 frames, provenance, physical-resolution evidence and typed acquisition failures. It has no person, PIV record, USB or vendor SDK dependency; record preparation remains in Fingerprints/Formats. Driver implementations are separate packages.

`CaptureDriverRegistry` verifies discovery metadata and applies one process-wide lease per canonical physical device identity, including across registry instances. Drivers must use the same canonical physical ID and enforce OS/device exclusivity across processes. Sessions serialize operations, have a finite lifetime of at most five minutes, propagate cancellation, and release leases only after driver cleanup. Cleanup failure quarantines the lease until restart. Drivers must honor cancellation and dispose partial opens before returning an error; forcibly releasing a device while native I/O continues is forbidden.

A raw frame cannot carry correction metadata or changed output geometry. Corrected frames require a SHA-256 calibration digest and correction version. Resolution is explicitly unknown, manufacturer-declared or physically measured; unknown resolution never becomes 500 ppi implicitly. Qualified acquisition requires a measured resolution and a named qualification profile. These are evidence assertions, not automatic verification of an external qualification record. Immutable frames contain no person identity.

The acquisition contract tests use a fake physical driver to exercise bounds, immutable copies, provenance, exclusive leases, cancellation, lifetime expiry, settings changes, repeated sequence values and disconnect cleanup. B1 adds no real device driver and makes no hardware qualification claim.
