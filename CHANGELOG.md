# Changelog

Releases follow Semantic Versioning. Version 3 is the .NET 10 consolidation release.

## Unreleased

- **Breaking:** every library now returns `Result<T, TError>`, `UnitResult<TError>`, or `Maybe<T>` instead of throwing or returning null. Errors are `ImagingError`, `FormatError`, `PivError`, `FingerprintError`, and `FacialError`, each with a stable `Code` from its `*ErrorCodes` class.
- **Breaking:** constructors that validated input are now `Create` factories returning results, including `FingerprintCapture.Create`, `FingerprintMatchingPolicy.Create`, `PivFingerprintRecordMetadata.Create`, `BiometricIdentity.Create`, `CbeffHeader.Create`, and `RgbImage.Create`.
- **Breaking:** `Incits378Codec`, `Incits385Codec`, and `CbeffCodec` `Parse` return `Result<ParsedRecord<T>, FormatError>`, with failure diagnostics on `FormatError.Diagnostics`, and `Write` returns `Result<byte[], FormatError>`. `InspectionField.Value` is `Maybe<string>`.
- **Breaking:** `BiometricSignatureWorkflow.SignAsync` and `VerifyAsync` return `Result<..., PivError>`. Provider failures and cancellation surface as `SigningProviderFailed`, `VerificationProviderFailed`, and `Cancelled`. The record and verification on a signing result are `Maybe` values.
- **Breaking:** `PivFingerprintEnrollmentService.Prepare`, `RankCandidates`, and `CreateRecord`, `FingerprintComparisonService.Compare`, `FingerprintRecordAdapter.ToMatchingTemplate`, `FacialRecordPreparer.Prepare`, and `PortraitPreparer.Prepare` return results. Their optional fields are `Maybe` values.
- **Breaking:** `RgbImage.Load` is now `RgbImageDecoder.Load`, and image transforms and encoders return results. The `RgbImage` indexer, `Mutate`, and `ProcessPixelRows` are removed; use `GetPixel` and `SetPixel`.
- BouncyCastle now replaces `System.Security.Cryptography` internally. This is not a public API change.
- Ship a single `LICENSE` file that includes the MediaPipe combination exception.
- Migrate tests to AwesomeAssertions.
- Limit every `.cs` file to 250 lines, every type to 200 lines, and every method to 40 lines, enforced in CI by `scripts/check-file-size.py`.
- Update the README, API guide, workflow guides, and examples to the Result-based API.
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
