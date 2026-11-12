# Live image policy delivery audit

This checklist separates implemented behavior from release acceptance. A test pass
for one source snapshot does not approve later policy changes. Normal publication
remains blocked until a passing `live-image-policy-v2.json` is available.

## Current delivery directions

- This is unreleased: use one current package and preference shape. Do not maintain
  migrations, old app-package readers or saved-policy compatibility shims.
- No InternalsVisibleTo. Production friend declarations have been removed and
  Studio tests exercise real public operator/service contracts.
- Have an independent sub-agent review test value after fingerprint integration
  and facial integration, and before the corresponding commits. Remove duplicate
  or implementation-specific assertions while retaining user behavior, failures,
  independent geometry, interoperability and performance contracts. Test counts
  are not a delivery target; record the coverage retained when merging tests.
- Latest bounded review merged two standalone contact-package setups into
  existing round trips, retaining default-off diagnostics, policy/statistics and
  missing-companion rejection. It removed duplicate FMR-byte and literal model
  marker assertions; interoperability goldens and invalid-marker behavior remain.
  Topology, qualification, native-call reuse, freeze races and tamper tests stay.
- After fingerprint accuracy and the attended trial pass, create focused local
  unsigned commits with `git -c commit.gpgsign=false commit`. Do not push.
- Then add built-in and USB webcam facial capture to the shared person workflow,
  consolidate FaceOFFx rules into Biometrics, and deprecate the old application.
  Reuse detection/cropping; implement the missing card-budget JP2 ROI and complete
  PIV CBEFF/signature-object path. Require actual issuer/operator/device evidence
  where image inference cannot establish conformance.

## Implemented and checked

The role-separated contact geometry and source-bound live qualification rewrite
passed the integration checkpoint recorded below. Later faint-contact and
second-finger recovery changes require a renewed integration run. Performance,
accuracy and attended operator acceptance remain separate.

- Shared assessment/extraction, immutable results and canonical views: preparation,
  PIV enrollment golden-output, coordinate and matching tests pass in the full suite.
- One acquisition loop, displayed-frame acknowledgment, tolerant motion and bounded
  recent candidates: capture and presence replay tests pass.
- Exact frozen selection, early adjustment and acquisition drain: three regression
  tests failed before the fix and pass in the full suite. Late preview and native
  results cannot replace the selected source.
- Intact live preview: tone-only preparation has no ridge-grid stencil or local
  equalization. Final preparation starts after acquisition drains.
- Adjustment drafts: accepted originals, cached processing/original scores, revisions,
  cancellation and guard changes have focused coverage.
- Person workflow: opposite-hand suggestions, stable editable plans, pending
  acceptance, retry preservation, reset and reference clearing have tests.
- Export/import: FMR quality, ordered views, metadata, hashes and the current
  contact, hole, feature and context companions pass the combined integration run.
- Material controls: Capture/Compare and the actual adjustment popup render at
  800x600, 1100x760 and 1280x880. Independent review found no clipped popup actions.
  Roboto, label bindings and 48-DIP targets are covered.
- Settings: Original/Reset, persisted adjustments and saved preferences have
  save/reload coverage. Invert remains display-only.
- Current public-contract preflight: all 857 normal tests across nine projects
  pass, including 275 Desktop and 187 Futronic cases. Release builds have zero
  warnings/errors; size and whitespace checks pass. The ignored persistent
  `artifacts/developer-checks/contact-contour-integration/post-ui-single-copy/release-verification.json`
  receipt pins nine test reports, the build log, five product assemblies and
  17 rendered screens. It includes the native slider keyboard/endpoint fixes
  and single-copy acquisition ownership. Later product edits require renewed
  integration verification.
- Independent test review retired nine unused support-hull product API files and
  five methodology-specific fixtures. Current connected-contact geometry retains
  the independent cardinal/grid-phase oracle and artificial/natural-edge checks.
  The manual-preview revision check extends the existing exact-native-image
  scenario, avoiding another overlapping capture setup.
- Context-only edits reuse the same assessments and unfiltered extraction;
  actual failing-to-passing regression evidence confirms native totals stay
  one NFIQ 1, one NFIQ 2 and one extraction. Details renders all eight distinct
  contact/feature/context mask roles. Imported metadata cannot authorize live
  reprocessing, and rejected public adjustments return their typed failure.
- Missing scanner runtime: a host-level regression first reproduced startup failure.
  The workspace now resolves and discovery returns a typed unavailable-runtime error.
  Runtime assets are restored after the test; no system libraries are modified.
- A physical clean-empty FS50 check retained all 120 frames over 30.20 seconds
  at fixed exposure 220. The reference became ready at sequence three (1.04 s)
  and stayed ready for all subsequent 117 frames, with no false contact
  qualification, acquisition error or alternating state. The ignored
  `artifacts/developer-checks/fs50-empty-stability-post-ui/verification.json`
  pins the scalar report and frozen harness/dependencies. Pixels stayed in RAM;
  no NIST calls or template/image files occurred. This covers empty glass only,
  not dirty glass, finger presentation, UI cadence or matching performance.
  A feature-free [bounded result](fs50-clean-empty-20261111.json) records the
  aggregate timings and exact product assembly identities.
- Release verifier: 22 tests cover regression, missing availability, incomplete
  source association, stale files and malformed evidence.
- Offline decoder: eight synthetic tests plus all 80 existing development images
  preserve exact samples. Identity-gray palettes are accepted without conversion;
  colored/nonidentity palettes and changed geometry are rejected.

## Faint-contact and second-finger recovery checkpoint

The current `fs50-phase-aligned-contact-v5` snapshot passes 888 normal tests
across nine projects, including 207 Futronic and 286 Desktop cases. The first
combined run exposed seven stale test assumptions; its reports remain preserved.
The renewed Desktop run covers the changed contracts. Formatting, size and
whitespace checks pass; both Release builds report zero warnings/errors.

The second-finger regression first reproduced a successful automatic first
capture, acceptance, and a second presentation stuck behind the removal latch.
Exact-frame confident absence now reaches the presentation gate while an exposure
reference warms. Rejected light contact and residue cannot satisfy removal, and
the accepted first capture survives the second capture and acceptance.

Seventeen actual rendered screens cover all three supported sizes. The separate
Preview bundles and audits 26 arm64 native binaries and passes strict signature
verification. Current preferences load without writes, preserving contrast 49
and the configured one-second delay. The source, test reports, rendered screens
and packaged assembly identities are pinned in the ignored receipt:
`artifacts/developer-checks/faint-contact-fix/integration-second-finger-v1/verification.json`.

The new ridge-extent veto was withdrawn after rejecting known good controls;
its measurements remain diagnostic. Existing C/W reference and fixed-anchor
checks remain enforced. The newly changed smear limitation is explicit in
[FS50 capture](../FS50-CAPTURE.md). This checkpoint does not approve matching
accuracy. LaunchServices launch succeeded, but the attended trial failed before
the first capture: surface-reference admission blocked the finger. The renewed
paired software preview gate also failed, 27.40 versus 15.37 frames/s, a 43.89%
rate decline; tone feedback passed at 3.70 ms p95. Exact public inputs, all twelve
batches and the packaged assembly identities are preserved under
`artifacts/developer-checks/public-preview-comparison/results-v1-phase-sampler-whole-w/`.
This snapshot is not an accepted operator or performance result. Spatial-only
admission and an output-preserving morphology optimization are being checked
separately; the older passing preview checkpoint does not cover these changes.

## First-contact recovery checkpoint

The subsequent source snapshot passes all 899 normal tests across nine projects,
including 211 Futronic and 293 Desktop cases. Release builds report zero warnings
and errors; CSharpier, size and whitespace checks pass. Its 17 supported-size
renders and tested/published assemblies are pinned in
`artifacts/developer-checks/first-contact-recovery/integration-v1/verification.json`.
The separate self-contained Preview bundles 26 audited arm64 binaries, passes
strict signature verification and launches without a settings error.

Healthy startup contact can now auto-capture without an empty-sensor primer.
Coherent faint contact can adjust exposure but cannot authorize capture until a
fresh raw image meets the capture checks. Spatial admission records unknown C/W
background measurements with no invented reference or anchor. Known matched or
anchored rejection survives exposure changes and acquisition gaps. Spatial-only
and exposure-only observations do not seed a fixed capture anchor. Six saved-file
round-trip/tamper cases verify these provenance distinctions.

Paired morphology reuses one integral buffer for both texture masks. All 57 public
and synthetic edge/radius inputs retain byte-identical complete mask/proof results.
The next unchanged paired software gate still fails: 22.89 versus 13.97 frames/s,
a 39.00% rate decline. Slider feedback passes at 3.943 ms p95; every display,
readiness and zero-native-call assertion passes with zero source/execution drift.
Its complete evidence is preserved under
`artifacts/developer-checks/public-preview-comparison/results-v1-paired-morph-spatial-current/`.
This software workload excludes USB acquisition and display scanout. Performance
and the attended first/second-finger trial remain unaccepted for this snapshot.

## Inspection update checkpoint

The inspection update passes all 304 Desktop tests, a zero-warning Release build,
CSharpier, size and whitespace checks. Thirty actual rendered screens include
normal/inverted markers and narrow, wide and edited-context fits. Independent
reviews found no remaining glyph, framing or source-pixel changes. The processing
assemblies are byte-identical to the first-contact checkpoint; canonical points,
angles, NFIQ and matching remain unchanged.

Endings use a hollow circle and one detached direction line. Bifurcations use two
symbolic lines around the single stored direction. A background-colored halo
improves contrast. New captures fit their surrounding context; Clear resets zoom
and pan. Same-source tone edits retain manual positioning, and Fit uses updated
context bounds. The existing two-slot journey now verifies two real empty frames
rearm capture without waiting for a background reference, while residue preserves
the latch and produces an actionable cleaning hint.

The self-contained Preview bundles 26 audited arm64 binaries and passes strict
signature verification. Source, test report, rendering and packaged-assembly
hashes are pinned in
`artifacts/developer-checks/inspection-polish-v1/verification.json`.
The inspection-polish Preview launched through LaunchServices and was observed
ready without a startup error. The user authorized future test-capture discard
and restarts without further confirmation. The earlier software preview-rate
failure remains unresolved; live glyph/auto-fit operator confirmation is pending.

## Policy investigation, not release approval

- Initial default processing failed: 230 to 176 genuine accepts in examined
  FVC2002 DB1_B, with two unavailable processing images.
- Seven fixed ablations isolate both contrast and mask losses. Connected original
  pixels still lose 17 diagnostic genuine accepts solely through feature removal.
- The failed candidate geometry used a convex hull of all measured selected support,
  with an explicit margin and inner guard. It preserves connected geometry without
  claiming that uncertain bridging pixels prove anatomy. Its use as feature
  authorization failed the prospective gate and must not be promoted.
- The four-arm development study retained all 240 images and all 28,680 pairs.
  Original pixels with hull feature admission improved genuine accepts from 747
  to 766 of 840, with zero of 27,840 non-mated accepts. Neutral whitening reached
  754; whitening plus guard 8 reached 764 but added one non-mated accept and fails.
  The exact neutral RawHull0 candidate is chosen before final evaluation. This is
  development evidence, not release approval; see the bounded summary in
  `uareu-development-summary-20261111.json`.
- The four-person held-out study completed with all 280 images and 39,060 pairs.
  RawHull0 improved genuine accepts from 859 to 865 but increased non-mated
  accepts from one to three and therefore fails. Its immutable source/input
  freezes and failed reports are preserved; see `uareu-final-summary-20261111.json`.
  The cohort is now diagnostic for revisions. FVC2004 remains reserved but has
  no verified person mapping. A fresh UNSW 2D research cohort has a verified
  pre-download 30/30/90-person split. All 6,000 nominal-500-ppi grayscale images
  pass archive/census/header/hash checks; 25 within-finger duplicate pairs are
  recorded without removal. Its final scores remain unexamined.

## Required before completion

- Recheck preview performance after the current capture fixes. The last frozen
  paired software workload passed: 23.26 old versus 22.12 current frames/s,
  a 4.90% decrease within the 10% limit. Tone feedback was 8.75 ms p95.
  The earlier 40.02%, 20.54% and 18.94% failed runs remain archived; they are
  not overwritten by this pass. The passing receipt is under
  `artifacts/developer-checks/public-preview-comparison/results-v1-detector-pooling-current/`.
  Its exact source/assembly freeze predates the latest faint-contact changes.
  These measurements exclude USB and physical display scanout. The last warmed
  five-frame native run measured 846.4 ms p95, also before the current fixes.
- Freeze and integrate a policy that preserves connected fingers, rejects residue,
  keeps native algorithms unchanged and does not lose recognition utility.
- Keep processing controls and confidence/display policy choices explicit settings;
  do not silently promote a failed research policy.
- Evaluate the frozen policy at threshold 40 with all attempts and unavailable
  images explicit, including the reserved person-separated final set. Report the
  small cohort and unknown linkage to earlier FVC participants honestly.
- Validate temporal best-frame selection separately from static-image accuracy.
- Recheck under-100ms feedback, at most 10% preview regression and warmed five-frame
  p95 below two seconds for the final implementation, with native-call counts.
- Recheck full build/tests, formatting, size, actual processed-image rendering,
  package round trips, native bundling and Finder launch for the final source.
- Complete the attended dirty/light-contact and ordinary-motion two-finger trial,
  accept/save/reopen the results and repeat comparison with the private reference.
  Keep all personal source images, templates and keys outside repositories.
