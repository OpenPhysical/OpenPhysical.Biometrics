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

Corrected frame `CalibrationDigest` is SHA-256 over the fixed-length concatenation
`descriptor[64] || controls[64] || calibration[4352]`. Together with
`CorrectionVersion = fs50-cubic-half128-v1`, this binds every correction input.
Raw frames preserve sensor bytes, have an empty correction digest/version, and
never claim correction. Both stages explicitly retain unknown physical resolution
and experimental qualification.

`Fs50ContactAnalysis` operates on unchanged corrected images. It applies the
prototype's 15-pixel local variance mask, 13-pixel closing, 7-pixel opening and
largest eight-connected component thresholds. This is a contact heuristic, not
liveness evidence. Integral statistics use exact integer sums; threshold-boundary
rounding can differ from the prototype's float box filter. Synthetic stripe
fixtures independently reproduce the saved prototype's component areas and
intensity percentiles.

`Fs50ExposureController.Next` returns an explicit recommendation. Automatic
adjustment requires contact and explicit enablement, honors a roll/exposure lock,
and changes exposure by at most 16. The caller applies the recommendation through
`ApplySettingsAsync`; no hidden update invalidates frame provenance. Display
contrast is outside this path. Rolled settings are rejected until the experimental
roll compositor is connected to acquisition.
