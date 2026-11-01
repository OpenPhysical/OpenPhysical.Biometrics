# Biometric processing and middleware boundaries

OpenPhysical.Biometrics owns biometric interpretation. OpenPhysical.Middleware
owns credential and card operations. Pivantra builds commercial workflows over
those public capabilities.

## Current package graph

`OpenPhysical.Biometrics.Formats` owns the canonical CBEFF, INCITS 378, and INCITS
385 models and wire codecs. `.Piv` depends on Formats and adds profile validation,
inspection, and the signature-service contracts. `.Fingerprints` depends on
Formats plus the independently versioned NBIS and NFIQ1 bindings. The root package
composes those entry points.

`.Facial` depends on Formats/Piv and CoreJ2K.Skia for captured-pixel preparation.
`.Imaging` owns buffers and Skia-backed image operations. `.Facial.Models` carries
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

The current OpenPhysical.Net solution contains CardEdge, CardManagement,
VirtualPiv, Middleware domain/infrastructure, CLI, emulator tests, and shared
credential services. These provide the essential PIV capability: transport,
PIN/security state, object envelopes, key access, CMS and trust services,
issuance transactions, and card readback.

Biometric adapters consume the public Formats/Piv packages. They map canonical
inspection into middleware presentations and supply cryptographic services to
the signature hooks. Enrollment inputs carry actual fingerprint/facial records
and acquisition evidence into provisioning. Middleware combines signature,
identity, trust, revocation, biometric profile, and comparison findings according
to its application policy.

PACS and the OSDP transport/ACU/PD projects remain reusable open source components.
Gp4Net, Kdf108, and other independent libraries keep their own release lifecycles.

## Commercial product ownership

Pivantra's product layer owns its commercial enrollment screens, operator
workflows, fleet administration, business integrations, and deployment experience.
It consumes the same OpenPhysical public APIs as other applications. Commercial
product code depends on the open source packages in that direction.

The final allocation of existing product-specific UI and orchestration classes
belongs to the product owners. Class-by-class migration follows an inventory of
their actual dependencies and rights coverage.

## Migration order

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
