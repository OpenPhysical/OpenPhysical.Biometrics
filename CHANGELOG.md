# Changelog

Releases follow Semantic Versioning. Version 3 is the .NET 10 consolidation release.

## Unreleased

- Write baseline TIFF resolution tags with the documented 72 ppi display default.
- Validate signature request lengths against the canonical CBEFF header size.
- Pin CI to the NFIQ revision with the shared runtime catalog.

- Add MediaPipe-backed `biotool face prepare`, reviewed head geometry, source-supported Full Frontal cropping, and shared JPEG 2000 encoding.
- Replace facial model assets with pinned Apache-2.0 MediaPipe ONNX models and add the first-party GPLv2 combination exception.

## 3.0.0-beta.1 - 2026-10-31

- Centralize canonical fingerprint-to-NBIS coordinate and angle conversion in the fingerprint package.
- Centralize INCITS 378 and PIV template conversion in the canonical fingerprint
  adapter, shared by the public API and `biotool convert`.
- Add named SD04 policy examples and negative different-finger workflow tests across capture/template combinations, including policy boundaries and immutable inputs.

- Correct INCITS 378 minutia wire types in the compatibility API: Other = 0, RidgeEnding = 1, and Bifurcation = 2. Canonical parsing and writing preserve these values.


### Added

- Canonical immutable INCITS 378, INCITS 385, and CBEFF models, codecs, located
  diagnostics, PIV profile checks, and structured inspection shared by the CLI.
- Explicit signature-service contracts, original signed-content preservation,
  bounded signature-length coordination, and independent verification findings.
- NBIS extraction/comparison, NFIQ 1 assessment, enrollment ranking, deterministic
  two-finger record preparation, and application-controlled matching policy.
- Captured sRGB facial preparation through CoreJ2K.Skia JPEG 2000 encoding.
- Owned Skia-backed image buffers and transforms for the facial pipeline.
- Workflow examples, migration guidance, mandatory API documentation, CSharpier,
  and isolated package-consumer CI across five supported native runtimes.

### Fixed

- Keep sensitive measurements, identities, paths, external diagnostics, and
  exception payloads out of operational logs.

- Preserve sRGB facial metadata and encode the complete big-endian quality field
  when adapting the existing facial writer to the canonical codec.
- Preserve image aspect ratios and coordinate mapping in facial model input;
  retain the requested rotation canvas dimensions.

### Changed

- Target .NET 10 with standard .NET and project-owned public contracts.
- Organize the modern packages and existing facial compatibility solution explicitly.
- Use SkiaSharp and CoreJ2K.Skia throughout imaging and JPEG 2000 paths.
- Align first-party licensing and contribution policies with the algorithm bindings,
  preserving original and dependency notices.
