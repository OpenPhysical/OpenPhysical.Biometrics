# FS50 qualification evidence

## Numerical compatibility

The admitted managed numerical profile is `fs50-cubic-half128-v1`. Its immutable oracle records OpenCV 5.0.0.93, NumPy 2.4.6 and Darwin arm64 as research provenance. The polynomial coordinate maps, input pixels, complete corrected images and coefficient representation have independent hashes. CI compares eight regenerated full-frame patterns, including high-frequency and pseudorandom content plus both ends of the zero-fill border; the original 20 horizontal ramp samples remain a separate vendor-observation check.

During independent numerical characterization, the managed arithmetic model matched 32 additional held-out 600,000-pixel random frames and 37 constant images exactly. These exploratory checks supplement the committed full-frame fixtures. Results apply to the frozen profile, not arbitrary floating coordinates or every upstream OpenCV implementation. Platform qualification runs the same immutable fixture hashes on each supported .NET OS/architecture before publishing that native capture package.

The new OpenCV cubic kernel source inherits Apache-2.0. It was not used as implementation source. Only its license header and dispatch behavior were inspected; coefficient/arithmetic recovery used common cubic mathematics and synthetic observed outputs. No Apache-derived source, vendor SDK, or captured fingerprint image enters the managed port.

## Physical release gate

Each supported scanner model/firmware and USB host combination records:

- Raw descriptor and controls provenance, original calibration SHA-256, crop offsets and numerical profile identifier.
- Orientation, coverage, physical scale measurement and calibration target evidence. Factory correction alone does not attest physical PPI.
- Capture quality across dry/wet fingers, supported exposure settings including 255, and representative rolled acquisition where enabled.
- Stable frame-rate/latency measurements, exclusive session ownership, bounded buffering, cancellation during a native transfer, disconnect/reopen and device recovery.
- Evidence that preview-only contrast never changes extraction/signing pixels and that corrected frames retain the exact transform provenance.

Numeric fixture passing does not close these hardware gates. Unsupported descriptor geometry, folded/reversed maps, incomplete sensor frames and unqualified physical resolution fail explicitly. Rolled capture remains experimental until its separate acquisition/quality qualification is recorded.
