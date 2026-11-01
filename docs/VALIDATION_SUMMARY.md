# Validation record

This consolidation targets .NET 10 and version 3.0.0-beta.1. Local validation
uses macOS ARM64. The runtime workflow repeats the package and managed checks on
Windows x64, Linux x64/ARM64, and macOS x64/ARM64.

## Managed contracts and workflows

The modern solution passes **135 NUnit tests**:

- Formats: 24: malformed/truncated records, length and extension framing, timestamp
  encoding, owned data, and canonical binary round trips.
- Fingerprints: 43: explicit finger/view selection, units, origin/angle/type/quality
  conversions, enrollment states, deterministic selection, immutable inputs, and
  image/template comparison workflows.
- PIV: 27: profile checks, safe inspection, exact signed bytes, bounded signature
  sizing, caller verification, identity binding, provider failures, and cancellation.
- Facial: 25: reference inference, EXIF orientation, roll correction, source coverage,
  reviewed boundaries, multiple faces, negative controls, concurrency, JPEG 2000,
  immutable inputs, and canonical record round trips.
- Imaging: 16: owned buffers, transformations, image formats, and TIFF alpha semantics.

The suites execute serially in the current solution.

## Reference fingerprints and application policy

The NBIS repository owns 20 attributed NIST SD04 images, their pixel/hash manifest,
independent NIST extraction results, and all 400 ordered score baselines. Its
native µnit suite covers 20 self-comparisons, 20 ordered paired impressions, and
360 ordered comparisons between different fingers. Plain, ASan, and UBSan checks
exercise those bindings and native failure paths.

Main uses that same fixture collection and loader. The named
`sd04-assortment-regression-v1` test policy accepts the selected paired control
and rejects two selected different-finger comparisons across all four
image/template combinations and a canonical selected-reference workflow. Boundary
tests verify the policy's inclusive threshold. Applications supply their own
versioned policy.

NFIQ's independent repository owns the original release/SD04 quality vectors,
algorithm/model provenance, native reference comparisons, sanitizer checks, and
managed quality-contract tests.

## Signature and card integration

OpenPhysical.Net passes 26 focused biometric integration tests covering canonical
adapters and real CMS signing, signature attributes, identity binding, trust,
and revocation outcomes. An additional emulator integration passes real detached
CMS signing → SCP03 card-object write → byte-identical readback → canonical
parse/profile checks → external signature and identity verification.

The emulator test uses an explicit fixture signer-trust policy. Trust and
revocation behavior have their own integration cases.

## Packages and developer gates

Eight current packages pass local clean-consumer validation. Checks cover notices,
model hashes, fixture/private-key exclusion, dependency separation, native
fingerprint operations, facial JPEG 2000 preparation, optional ONNX inference,
and installed CLI inspection/conversion. CLI rejection cases cover malformed
records and preservation of an existing output file.

Release CLI packing requires all five NBIS/NFIQ1 native runtimes. Local package
checks use `AllowPartialBiometricRuntimes=true` explicitly for the current host.
Algorithm repository pipelines produce and consume their complete runtime
packages on each supported platform.

CSharpier checks the current source tree. Eight positive/negative documentation probes verify
that documented APIs compile and that missing summaries/parameters/returns,
broken references, diagnostic suppression, downgraded warnings, and disabled
analyzers fail the build. Public XML documentation and warnings-as-errors remain
normal build gates.

## Reproduce

Build pinned algorithm dependencies first, then run the checks serially:

```sh
python scripts/build-development-dependencies.py
dotnet tool restore
dotnet csharpier check .
python scripts/verify-models.py
python scripts/verify-documentation-gate.py
dotnet test OpenPhysical.Biometrics.sln -c Release -m:1 --collect:"XPlat Code Coverage" --results-directory artifacts/test-results/modern
python scripts/test-package.py
dotnet run --project examples/OpenPhysical.Biometrics.Examples -c Release -- --sd04 ../OpenPhysical.Biometrics.NBIS/tests/fixtures/sd04
```

CI retains managed coverage, packages, and per-runtime results. Native coverage
and complete algorithm-package consumption remain with their owning repositories.
