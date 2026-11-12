# FS50 capture and numerical profile

## Factory geometry

The managed FS50 geometry path accepts the observed 1280 × 832 sensor layout and correction version `8f`, producing an 800 × 750 grayscale image. Descriptor orientation/version bytes, crop offsets, coefficient bounds, sensor coverage, positive axis gradients and local Jacobian bounds are checked before a map is admitted. Signed division truncates toward zero; polynomial shifts retain signed flooring. The factory routine's constant coefficients remain unused.

Factory calibration does not establish measured physical sampling density. Frame provenance binds the original calibration bytes, descriptor, crop controls, device identity and numerical profile. Changing crop controls changes the map even when the calibration SHA-256 remains unchanged. A capture driver records those inputs together rather than treating a calibration digest alone as the complete transform identity.

## Stable interpolation profile

`Fs50Resampler.ProfileId` is `fs50-cubic-half128-v1`. Factory maps consist exclusively of exact 1/128-pixel coordinates. The resampler accepts the validated `Fs50GeometryMap`, not arbitrary float maps. It uses four-tap cubic convolution, binary16 coefficient bit patterns, one binary16 rounding per fused multiply-add in horizontal and vertical passes, ties-to-even byte conversion, zero border fill, then grayscale inversion. Raw sensor pixels remain unchanged.

The frozen 128 × 4 coefficient bit patterns have SHA-256 `b6d1a9297244b6f68adb198525339474e3f610531cac4347fa1306c043849c5a` when serialized as little-endian unsigned 16-bit values. The managed implementation evaluates each bounded binary16 product/sum through double precision before the binary16 rounding, avoiding dependence on optional native Half SIMD instructions.

These numerical constants were independently recovered from synthetic observable outputs of the saved prototype's development dependency, `opencv-python-headless==5.0.0.93` on Darwin arm64. Recovery used 8,192 synthetic 1D rows and held-out full-frame checks. No OpenCV cubic implementation source was copied or translated. OpenCV is neither linked nor distributed as part of this capture library. The oracle tool is a separate development tool.

The previous assumption that this prototype used a 1/32-pixel Q15 table was incorrect. OpenCV 5's floating-map cubic path preserves the factory map's finer coordinates. Applying the older table profile changes many bytes on a textured synthetic image. This profile freezes the observed byte behavior; it does not promise equivalence to every OpenCV version, optimization path, pixel type or CPU architecture.

## Reproducible evidence

`tests/OpenPhysical.Biometrics.Capture.Futronic.Tests/Fixtures/fs50-resampler-half128-v1.json` contains complete corrected-frame hashes, source-frame hashes, selected numeric samples, coordinate-map hashes and oracle provenance. The eight independent frames are three precisely specified xorshift32 streams, horizontal and vertical ramps, a checkerboard, and two additional xorshift32 streams with synthetic valid calibrations that reach the near and far zero-fill borders. The border calibrations use crop/scale pairs `(1,1)/(10008,10008)` and `(255,82)/(15420,9995)`; their exact coefficients, controls and map bounds are retained alongside their hashes. Tests regenerate source bytes and compare every corrected byte through SHA-256. No fingerprint image is included.

`tools/fs50/generate_resampler_oracle.py` regenerates the numerical oracle using the pinned development-only OpenCV package and the redistributable calibration fixture. It evaluates the factory polynomial independently and calls OpenCV on synthetic pixels; it does not derive expected image hashes from the managed implementation. Updating the coefficient profile or oracle version requires a new reviewed profile identifier.

Physical scanner qualification, image quality and measured scale remain governed by [FS50 qualification](FS50-QUALIFICATION.md).

## Driver and session integration

`Fs50CaptureDriver` implements the shared capture driver contract. Register an
`IUsbDeviceProvider` configured with the approved absolute libusb path, then call
`AddFs50Capture`. Registration does not discover or open hardware. Use
`CaptureDriverRegistry` for shared process-wide device leases; the native provider
also exclusively claims interface zero. Reconnect requires discovery of the new
USB address rather than reuse of a stale device descriptor.

Opening reads and validates the descriptor, controls, two identical calibration
passes and the complete geometry map. The session bounds opening and capture by
its original lifetime, serializes captures and setting changes, and closes on a
failed or cancelled acquisition. Settings apply to the next capture command;
FS50 carries its exposure byte in that command, including manual zero or 255.
Changing settings never extends the session deadline. Idle lifetime cancellation
also releases the transport without waiting for the caller to capture again.

### Reply ownership and clean reopening

The command/reply lock stays owned until the complete issued reply is consumed.
Caller cancellation still returns `capture.Cancelled`, but cleanup uses its own
two-second deadline rather than the already-cancelled session token. A cancelled
image is discarded, including when cancellation precedes the first image byte.
Only cleanup accepts partial chunks; normal capture retains its exact response
length checks. Reads are bounded by a transaction deadline and a 64-read limit.

A fresh transport drains its IN endpoint before sending any descriptor command.
Synchronization has a two-second deadline, a 64-read limit, and a fixed byte
budget. Only a zero-byte USB timeout proves the endpoint quiet. A timeout can
still carry transferred bytes, as described by the
[libusb synchronous-I/O contract](https://libusb.sourceforge.io/api-1.0/group__libusb__syncio.html);
the native edge preserves those bytes for bounded draining. A disconnect is not
treated as a quiet endpoint. Failure to synchronize returns
`capture.SynchronizationFailed`; descriptor signatures and both identical
calibration passes remain mandatory.

The 2026-11-10 live empty-platen regression reproduced alternating
`UnsupportedGeometry` and `CalibrationInvalid` errors after abandoning the first
64 KiB of an image. Both caller cancellation and an advanced five-minute session
timer reproduced the sequence. With reply cleanup, each case consumed all 17
chunks (1,064,960 bytes), returned cancellation, and passed four consecutive
fresh descriptor/calibration opens. A 360.23-second real-time soak then acquired
1,485 empty-platen frames, crossed the actual 300-second expiry, reopened with
validated metadata, and continued with zero unexpected failures. Only aggregate
acquisition metadata was retained by the diagnostic harness; no image files were written.

Corrected frame `CalibrationDigest` is SHA-256 over the fixed-length concatenation
`descriptor[64] || controls[64] || calibration[4352]`. Together with
`CorrectionVersion = fs50-cubic-half128-v1`, this binds every correction input.
Raw frames preserve sensor bytes, have an empty correction digest/version, and
never claim correction. Both stages explicitly retain unknown physical resolution
and experimental qualification.

`Fs50ContactAnalysis` operates on unchanged corrected images. Its candidate mask
uses 15-pixel local variance above 90 and mean below 242, 13-pixel closing,
7-pixel opening and eight-connected components. Integral statistics use exact
integer sums; threshold-boundary rounding can differ from the prototype's float
box filter. Synthetic stripe fixtures independently retain the saved prototype's
component areas and intensity percentiles.

`Fs50ExposureController.Next` returns an explicit recommendation. Automatic
adjustment requires contact and explicit enablement, honors a roll/exposure lock,
and changes exposure by at most 16. The caller applies the recommendation through
`ApplySettingsAsync`; no hidden update invalidates frame provenance. Display
contrast is outside this path. Rolled settings are rejected until the experimental
roll compositor is connected to acquisition.

The four-argument controller retains its target signal of 190. The overload with
an explicit target validates values from 1 through 255. The desktop starts at
exposure 220 and targets signal 210 with a 195–225 deadband. Signal is 255 minus
the foreground's fifth-percentile gray value, so larger values mean darker
acquisition. The controlled empty-platen hardware vectors independently show
mean gray falling from 245.88 at exposure 200 to 243.50 at 255. These settings
change the sensor acquisition; they do not remap an existing image's pixels.

## Live foreground and preview cost

Foreground analysis counts separate substantial connected regions. With default
`Fs50EvidencePolicy` settings, candidates cover at least 4,000 pixels and span at
least 40 by 80 pixels. Candidates whose fifth-percentile gray is above 205
are rejected for capture as `TooLight`. A separate faint-texture pass (variance
above 9 and mean below 252) also finds the lighter boundary around a strong core.
Original-pixel ridge evidence constrains connected growth into that boundary;
light texture alone does not authorize capture.

Each remaining candidate requires local patches of coherent, repeated ridges.
Overlapping 32-pixel blocks use Sobel gradients and orientation coherence of at
least 0.35. Profiles across the ridges must agree along the ridge direction,
span at least 12 gray levels, alternate at least four times, and repeat with a
6–16-pixel period. Three neighboring supported blocks must have periods within
two pixels. This authorizes the contact component once; every locally valid
window remains in the separate ridge-support evidence. Parallel profiles are
aligned before averaging and checked again over all 32 actual source samples.
Nearest-pixel sampling uses one consistent tie rule and geometry-derived safe
bounds. These checks do not change or resample the acquisition image.
These thresholds are validated application settings, not NFIQ quality scores. NIST's
[quality feature definitions](https://www.nist.gov/system/files/documents/2021/10/06/nfiq-2_quality_feature_defin-ver05.pdf)
describe the separate orientation-certainty and ridge/valley-clarity concepts.

Ridge-support windows provide evidence, not a stencil for the fingerprint image.
The selected contact C remains connected; after capture, its enclosed holes H
are filled to define F = C ∪ H. Context E surrounds the contact but never
authorizes exterior template features. Original mode uses F for eligibility;
adjusted pixels use F intersected with the actual artificial-cut guard of E.
A separate whole-contact check evaluates both C and the complete observed weak
component W when a matching empty reference or a previously reference-qualified
fixed anchor is available. Its 4,000-pixel static or uncorroborated exterior rule
remains unchanged. Missing background evidence does not require an initial
empty-sensor ritual: `SpatialCapture` admits an original observation with one
accepted contact, one spatial component, qualifying ridge evidence, no contact
issue and sufficient signal. Both C/W photometric counts remain explicitly
unknown; reference, reference-key and anchor fields are absent.
Ridge-support extent is retained as a diagnostic; it does not trim C/F or
independently veto capture.
Expansion, guard and detection thresholds are configurable application policy in
`Fs50EvidenceOptions`; they are not NIST requirements.

Rejected regions return `TooLight` or `InsufficientRidgeEvidence`, zero capture
area/count and an all-zero accepted mask. A coherent `TooLight` observation
retains its separate original spatial C/W evidence. Without a reference or
qualified anchor, a single coherent light region receives `SpatialExposure` for
bounded automatic exposure correction only. Capture requires a later original
frame that passes the darkness and contact checks. Neither spatial-only admission
nor a light exposure-only observation creates a capture-qualified anchor.
Regions without qualifying ridge evidence, multiple contacts, and known rejected
C/W exterior cannot drive exposure. `Diagnostics` preserves separate candidate, ridge-support,
accepted, rejected and
inner planes, plus original photometric statistics for every candidate. All masks
have top-left row-major coordinates; one means included. The source Gray8 hash,
complete policy and version bind these immutable observations to their image.
Clean absence has
`Issue=None` and `Present=false`; usable contact has `Issue=None` and
`Present=true`. Tests cover random and directionally stretched speckles, uniform
smears, pale ridge-shaped residue, 20 public SD04 impressions, partial/dry-looking
public prints, and five previously acquired clean empty-platen frames.

This image heuristic does not establish physical contact, fingerprint authenticity
or liveness. A newly changed large smear containing one locally qualifying ridge
patch can pass component detection and reference-change checks. Without a
compatible background comparison, coherent residue can also resemble a finger.
The retained ridge-extent diagnostic exposes that limitation; it is not a stronger
rejection guarantee. Pure insufficient-ridge noise and multiple contacts remain rejected.
Substantial static or unresolved C/W exterior remains blocking when established
by compatible reference or fixed-anchor evidence. No matching-accuracy
approval follows from this admission policy.

Live tone preview stays intact, without a segmentation outline or stencil.
After capture, the outline uses that frozen frame's feature region F in original
image coordinates. Foreground selection does not crop, rescale, or alter the
original acquisition; final processing pixels are displayed before extraction.

Integral statistics and morphology reuse cleared pooled scratch arrays. A warmed
empty-frame allocation regression bounds analysis below 4 MB. On Darwin arm64 in the
2026-11-10 Release benchmark, median corrected-image remapping fell from 97.62 ms
to 12.96 ms. Foreground analysis took 9.66 ms and allocated 1.80 MB per frame,
compared with 10.16 ms and 35.83 MB before pooling. Gray8 bitmap presentation took
0.49 ms. The independent numerical and hardware fixtures retain complete-byte
parity coverage.

The per-preview evidence and image-preparation stages do not invoke NBIS or NFIQ.

Raw USB acquisition remains exposure-dependent: the same scanner measured median
85.8, 133.1, 188.1, and 229.5 ms at exposures 64, 128, 200, and 255 respectively.
These transfer times are separate from correction, segmentation, and presentation.

## Processing artifacts and empty-sensor learning

`FingerprintPreprocessing.Process` takes an immutable acquisition, validated
retained/inner planes, a snapshotted policy, processing revision and cancellation
token. Its artifact owns processing Gray8 pixels, preserves the original frame,
and records image/mask/policy hashes and a provenance-bound identity. Geometry is
unchanged. Original mode, with no effective local enhancement and neutral
brightness and contrast, returns the complete source pixels exactly. A zero
local-normalization blend is neutral even when its enable flag is set.

Adjusted mode whitens excluded pixels. Managed CLAHE uses retained pixels only,
32-pixel tiles, a two-times uniform-occupancy clip limit and bilinear interpolation
between tile centers. The blend is a policy setting, zero by default; partial
edge tiles use their actual centers. Brightness then adds an offset from minus
64 to 64; white-anchored contrast applies gain 1–2.5, desktop default 1.3. Excluded background is
restored to white. Controls and normalization parameters are validated policy,
with independent cumulative-histogram and interpolation tests.

The separate masks and inner guard follow the architectural distinction in
SourceAFIS [filtered masks](https://sourceafis.machinezoo.com/transparency/filtered-mask),
[inner masks](https://sourceafis.machinezoo.com/transparency/inner-mask) and
[equalized images](https://sourceafis.machinezoo.com/transparency/equalized-image).
This implementation does not import SourceAFIS code or thresholds and does not
change NBIS/NFIQ algorithms. Metadata-only identity and current policy-digest
checks verify saved evidence without rerunning preprocessing. The desktop applies
contrast 20 by default, retains exact Original mode, and preserves saved choices.
The feature area uses connected contact with enclosed holes filled. The context
envelope never authorizes exterior features. Local normalization remains an
advanced operator choice; darker pixels alone are not evidence of better matching.

`Fs50EmptySensorLearning` retains references in RAM only. Defaults require four
distinct stable empty acquisitions spanning at least 750 monotonic milliseconds,
no substantial candidate or ridge-supported region, and mean gray at least 230.
All learning thresholds are validated `Fs50EmptySensorOptions`; rejected or dark
unclear frames never establish a reference or qualify as empty removal evidence.
The five existing clean hardware vectors have mean gray 243.5–254.5.

References are keyed by device, caller-supplied open-session identity, driver,
calibration/correction, geometry and exposure. At most two exposure references
are retained by default. Clear, reconnect, calibration change and shutdown
release the references. Learning happens opportunistically; it does not block
the first valid spatial-only capture. A capture qualified against a matching
reference may establish a fixed anchor for ordinary exposure changes. Such frames
have no photometric difference or matching reference when that exposure is absent.
A known static or unresolved rejection persists across exposure changes and gaps,
so changing exposure cannot escape it through spatial-only admission. Two genuine
empty observations, a new scanner session or changed calibration/driver/geometry,
or a successful actual matching comparison clear that rejection. Stored admission
metadata cannot recreate the runtime source-bound observation required for live processing.
Reference pixels are never subtracted from an original or processing image.

A warmed Darwin arm64 benchmark measured public-fingerprint adjustment median
6.79 ms and p95 11.51 ms. New evidence analysis measured median 21.37 ms;
analysis, mask construction and preprocessing together measured 29.50 ms
(p95 31.24 ms). An independently retained older variance/component implementation
measured 13.52 ms on the same pixels. Clean glass measured 15.22 ms for new
analysis and 25.40 ms including preparation. These are software timings, not a
live scanner frame-rate measurement.

An empty-platen hardware replay at exposure 220 then acquired 88 frames without
contact or protocol failures. The RAM-only reference became ready at warmup
frame five; adjacent-frame mean absolute difference was 0.3795 gray levels
(p95 0.3828), below the default stability limit of two. Alternating two 20-frame
phases per implementation measured 3.9178 frames/s with the pinned pre-plan
analyzer and 3.7192 frames/s with evidence, reference learning and preprocessing,
a 5.07% reduction. This acquisition/preparation loop excludes UI rendering.
Only scalar timing/evidence statistics were retained; no image was saved.
