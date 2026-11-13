# Studio facial capture validation

Date: 2026-11-12. This records unsigned staging behavior, not PIV station
certification or fingerprint capture-policy release approval.

## Automated gates

- Release solution build: zero errors and warnings.
- Full solution tests: 1,050 executed, 1,050 passed, zero failures. Explicit hardware
  and performance cases are opt-in and are not included in that execution count.
- Camera-readiness follow-up: 20 focused face tests passed after guarding capture
  during opening and moving button notification after the first displayed-frame
  acknowledgement. The bound-button regressions failed before those fixes.
- CSharpier: 1,173 files checked. Repository file/type/method size checks pass.
- Installed portrait CLI rejection smoke passes against the final Tool build.
  Positive CLI encoding/OpenJPEG/atomic-installation claims are not part of that
  smoke: its public smiling fixture now correctly fails the mouth check. Shared
  encoding, transaction and codec round-trip coverage remains in the .NET tests.
- Native camera bridge: strict ARC compilation with Wall/Wextra/Werror; six C ABI
  exports checked. Read-only metadata probe preserved active device formats,
  color space and frame durations, with one available device and 14 modes.
- Publisher bundles and audits 28 arm64 native binaries, copies model/native/font
  notices, and verifies a deep, strict local ad-hoc signature.

Public-fixture regressions cover exact displayed-frame capture, source opening
and drain, stale error rejection, stable source/mode selection, repeated preview
notifications, one shared identity, independent retries, explicit Apply/Accept,
uncalibrated unsigned preparation, accepted acquisition receipt snapshots,
encrypted save/clear/reopen, and unchanged accepted bytes when guides toggle.
Independent cryptographic vectors and malformed authenticated archive tests run
through the portable package reader/writer.

The geometry guide regression checks the independent 192/320 output eye-band
coordinates, actual detected-eye anchors, advisory ROI, and projection into the
original source. PIV guides are absent during preview and appear only after
capture. Their thin red dimensions and quiet green ROI have no numeric labels.
Two independent reviewers checked review, adjustment and retake screens at
800x600, 1100x760 and 1280x880. Numeric entry remains a flyout.

Source-pose validation blocks acceptance for estimated pitch, yaw or roll at or
beyond five degrees; unknown pose also requires a retake. Independent
physical-projection fixtures cover signed and combined rotations and both image
aspects. A perspective counterexample guards against using two-dimensional eye
tilt as an additional rejection rule. Pose is cached with analysis, reused during
crop adjustment, and retained as estimated evidence with its virtual-camera
field of view, policy identifier and canonical-model hash. The PIV record's pose
bytes remain unreported. A rejected but frameable image retains its cropped
inspection pixels, transform and guides. A separate red failure line names all
failed checks. Explicit Override failure enables review without accepting or
advancing. It is limited to eligible automatic checks and never permits missing
pixels, unsupported framing or invalid encoded records.

Mouth closure uses cached landmark measurements, with a configurable four-percent
application tolerance. Settings reassessment reuses the exact inspection pixels
and cached measurements. It invalidates stale review/overrides, preserves accepted
objects and handles the geometry editor's Cancel baseline. Mouth closure is no
longer an attended checkbox claim; live guidance prompts for closed lips.

Encrypted save/clear/reopen tests verify the original failed checks, source-bound
override time and codes, actual mouth policy and measurements, and separate
portrait-review evidence. Overridden captures retain false full-frontal evidence
and its acquisition finding. Strict CLI preparation does not honor overrides.

## Settings and required-input follow-up

The complete Desktop suite passed 367 tests after automatic settings recovery and
required-input feedback were added. Three focused rendered regressions passed
after the final notification contrast adjustment. CSharpier checked 1,180 files;
file/type/method limits and whitespace checks passed.

Invalid or unreadable settings are replaced with persisted defaults. First run
remains quiet. A nonblocking Material notice explains recovery; precise read,
write and application failures remain in logs. Application failures take
precedence over recovery notices and failed writes never claim persistence.
The missing person name stays red both focused and unfocused. The unchecked
portrait-review checkbox also stays red until confirmed. Independent review of
the public-fixture renders found no actionable visual issues.

The signed-for-local-use Preview bundle for this follow-up is staged under
`artifacts/review-apps/`. It has not replaced the running application because an
unsaved facial capture was observed and restart/discard confirmation is pending.
Photo auto capture and Google person segmentation are not implemented by this
follow-up. Segmentation feasibility was assessed; background replacement remains
separate from using an analysis mask over unchanged photographic pixels.

## Attended hardware evidence and remaining checks

The initial FlashCap macOS path exposed format mutation during enumeration,
misreported frame layout, and native ownership crashes. It is not used by Studio
on macOS. The replacement AVFoundation bundle launched via LaunchServices and
showed an upright built-in 1920x1080/30 preview with a populated mode selector;
the operator captured a frozen frame. No personal camera frames were written to
the repository by that check.

The built-in camera also delivered a frozen capture rejected for estimated pitch
5.2 degrees. Its absent crop/guides exposed the inspection/acceptance coupling,
now resolved by shared framing and covered by rendered and record-readiness tests.
The final override/mouth/guidance update is built for the next attended check.
Built-in mode-switch stress, actual OS lock/unlock behavior, and a complete
private face-plus-two-finger accept/save/clear/reopen trial remain to be observed.
External USB camera testing is explicitly deferred by the operator.

## Artifact locations

Automated TRX evidence and public-fixture renders are under the ignored
`artifacts/developer-checks/facial-integration/` directory. The self-contained
bundle is `artifacts/apps/OpenPhysical Biometric Studio Preview.app`.
Private operator packages remain outside the repository in persistent private
application storage. Unsigned staging retains unknown optical evidence instead
of inventing proof; camera setup is recommended and does not gate Studio saving.
