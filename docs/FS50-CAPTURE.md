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
