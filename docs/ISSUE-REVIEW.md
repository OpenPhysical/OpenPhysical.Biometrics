# Issue review: October 31, 2026

The eleven reports reference revision `9bd5497`. This review applies them to the
current MediaPipe and canonical-format working tree.

## Current implementation

- [#1](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/1): The clean
  break retires `QualityScore`, `Iso19794Assessment`, and their compatibility
  projects. Current quality APIs expose typed numeric assessments.
- [#2](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/2): The clean
  break retires the compatibility CMS verifier and verification command. The
  current caller-supplied verifier returns separate signature, envelope, trust,
  revocation, and authenticated-identity findings. Contract tests preserve both
  failed and unevaluated trust results for application policy.
- [#3](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/3): The clean
  break retires `BerTlv` and its compatibility consumers. The canonical CBEFF
  parser validates unsigned lengths against the complete input before slicing.
  Regression cases exercise `0x80000000` and `0xFFFFFFFF` lengths.
- [#4](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/4): The clean
  break retires the compatibility CMS signer and verifier. Current signature
  requests carry the typed card UUID and the entryUUID attribute requirement.
  Existing tests exercise identity preservation and mismatched-card rejection.
- [#5](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/5): Baseline
  TIFF exports include sorted XResolution, YResolution, and ResolutionUnit tags.
  Pixel buffers use a documented 72 ppi display default. Tests read the directory
  and rational values directly, alongside pixel round-trip coverage.
- [#6](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/6): Signature
  requests use `CbeffHeader.Size` for validation and diagnostics. PIV OIDs have one
  production definition in `BiometricSignatureRequirements`; the clean break
  retires the compatibility inspection command containing duplicate literals.

- [#7](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/7): The clean
  break retires the target-byte strategy and its decimal-rounded retry loop.
  `PortraitEncoding` uses an explicit invariant 1.6 bpp encoder target, checks the
  actual output against the 15:1 limit, and has a bounded lossless retry.
- [#8](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/8): The clean
  break retires `ProcessingMetadata.CompressionRate` and its competing formulas.
  Portrait preparation returns encoded pixels and typed findings; compression
  validation uses the final encoded byte count and source RGB byte count.
- [#9](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/9),
  [#10](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/10), and
  [#11](https://github.com/OpenPhysical/OpenPhysical.Biometrics/issues/11): The
  clean break retires the compatibility certificate stores, test-anchor
  registrations, resource certificates, and store interfaces. Applications
  supply trust evaluation through `IBiometricSignatureVerifier`, with independent
  certificate-path and revocation findings.

## Validation

Run the solution tests serially with `dotnet test OpenPhysical.Biometrics.sln
-c Release -m:1`, then run CSharpier and the documentation-gate script. The TIFF
regression first reproduced the missing tags against the original encoder.
Repository CI pins the updated NFIQ runtime-catalog revision.
