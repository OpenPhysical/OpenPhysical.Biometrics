# OpenPhysical Biometric Studio

OpenPhysical Biometric Studio captures a selected finger from a Futronic FS50,
exports an unsigned ANSI/INCITS 378-2004 minutiae record, and inspects and compares
saved records. It uses the shared FS50 acquisition path, NIST NBIS extraction and
Bozorth3 matching, NFIQ 1 classification, and supplemental NFIQ 2 image quality.

## Build and launch on macOS

The new live-image policy is under validation. Its first fixed public matching
gate failed: 230/280 genuine acceptances on the historical pipeline versus
176/280 on the processed path, with zero impostor acceptances in 2,880 comparisons
for either path. Normal app publication is blocked while this is investigated.
The existing installed app remains unchanged. `--candidate` creates a separate
Preview bundle for review; it does not approve the policy for release.

The publisher requires macOS, the .NET 10 SDK, Python 3, and Xcode command-line
tools. The pinned `UsbDotNet.LibUsbNative` NuGet dependency provides the arm64
libusb runtime and its license. The finished app does not require Homebrew,
a separate libusb installation, or an installed .NET runtime.

```sh
./scripts/publish-desktop-macos.sh
open "artifacts/apps/OpenPhysical Biometric Studio.app"
```

For an explicitly separate review build, use `--candidate`. `--no-restore` uses
already restored runtime assets. The publisher includes the policy validation
record in the bundle. A passing schema2 release record must bind every relevant
acquisition, processing, format and quality-provider source file and the fixed
threshold 40. Its complete pair ledger retains all attempts, records permitted
image inability as a null score and nonacceptance, reconciles availability and
acceptance counts, and contains zero operational errors. Genuine acceptance must
not decrease and non-mated acceptance must not increase on the full denominators.
New or changed source files invalidate approval; a passing flag alone is
insufficient. The [prospective protocol](FINGERPRINT-RELEASE-PROTOCOL-V2.md)
defines these rules without changing the original failed schema1 study.
Candidate bundles receive a current-source, explicitly non-passing research
record, rather than reusing historical validation evidence.

The result is a self-contained `osx-arm64` app. The publisher bundles libusb,
NBIS, NFIQ 1, NFIQ 2 and its native dependencies, the .NET runtime, package license
notices, the fingerprint Dock icon, and this guide. It checks
native architectures and dependencies, rewrites local dylib references, removes
development-directory runtime search paths, and verifies a local ad-hoc signature.
The app can be launched from Finder without relying on the working directory.

The session keeps person details in one shared model. Its registered fingerprint
section owns the required finger plan, accepted captures, and package writer.
`IEnrollmentSection` gives each section a stable identifier, display model,
completion state, reset operation, and typed saved-artifact result.

## Capture a finger

1. Enter a **Name or label** for this person. An alias is sufficient. Add an optional
   identifier when it helps organize local test captures.
2. Choose the required fingers. Each new person starts with one suggested finger
   from each hand; the suggestions stay fixed until **Clear person**. Edit either choice,
   or turn off **Two fingers** for a single-finger record.
3. Connect one FS50. The utility discovers it automatically, including when it is
   attached after launch. You can place a finger immediately. A surface check learns
   automatically in the background whenever the platen is clear, using RAM only.
   A missing surface reference does not block an otherwise valid finger. Dirty,
   faint or unclear contact cannot establish an empty-sensor reference.
4. Position the indicated finger on the platen and adjust exposure if
   needed. Acquisition starts at exposure 220. Automatic exposure uses the existing
   FS50 contact analysis with a darker target; manual exposure retains the full 0–255 range.
5. **Auto capture** is on by default. The empty platen stays hidden. Keep one finger
   present for the configured delay, initially one second. The utility retains at most
   five acknowledged images from the latest 750 milliseconds. At the deadline it selects
   a baseline using brightness-normalized, position-compensated motion, then
   foreground clipping, reliable ridge coverage and recency. It assesses this bounded
   burst with both NFIQ engines using one fixed image-adjustment policy. An alternative
   must be no worse on either score and improve NFIQ 1 by one class or NFIQ 2 by five
   points. Conflicting improvements retain the baseline. Neither score is averaged.
   Only the selected image receives a separate NBIS extraction.
   Small movements and automatic exposure changes affect ranking without restarting
   the delay. Two consecutive major position changes or two absent observations clear
   the interval. A gap longer than one second, or changed device, image geometry,
   correction, calibration, or resolution evidence also clears prior candidates.
   Multiple fingers clear the interval and cannot trigger capture. Turn auto capture off to use **Capture now**
   manually, which freezes the latest acknowledged image. Both paths retain the exact
   selected image, timestamp, foreground mask, and acquisition settings for one extraction.
   Very light or unstructured contact does not start the countdown. The workspace
   asks the operator to clean the sensor or reposition the finger; dry skin can
   also produce light contact. Qualified light contact can remain visible while
   automatic exposure adjusts; it stays ineligible for capture. Unstructured residue
   cannot drive exposure.
   Manual capture is enabled only for a visible, usable single-finger image.
6. Review the frozen image, finger-area boundary, selected minutiae, and quality.
   A completed capture shows **Waiting for acceptance** until **Accept finger**.
   Lift the finger before placing the next one. Two confirmed empty frames rearm
   auto capture even while a new exposure reference is warming; light contact and
   residue cannot count as removal.
   **Reset capture** clears this attempt and rearms automatic capture, including when
   the finger remains on the platen. Retrying an accepted slot makes that slot pending
   again and blocks saving until its replacement is accepted, while retaining the other finger.
   **Accept finger** retains this finger and advances
   the plan; lift the previous finger before placing the next one. Previously accepted fingers remain intact.
   Display overlays do not alter processing pixels.

NFIQ 1 classes include their meaning: **1 (excellent)**, **2 (very good)**,
**3 (good)**, **4 (fair)**, and **5 (poor)**, using the
[NIST quality definitions](https://www.nist.gov/system/files/documents/2021/10/13/nfiq_ir_7151a_dataset.pdf).
The encoded template-quality number remains available in **Details**.
NFIQ 2 is shown separately as a **0–100 score (higher is better)**. It is assessed
on each bounded candidate after freezing; the selected result is cached and saved
as supplemental per-view metadata. It
does not replace the NFIQ 1 classification or change the PIV quality encoding.

## Image adjustments

**Image adjustments** previews brightness and contrast on the intact acquisition.
Presence/motion estimates remain internal; live preview never uses ridge-support
tiles as a clipping stencil. Segmentation and optional local enhancement run only
after acquisition has frozen and drained. NIST receives the final displayed
processing pixels, not a hidden alternative image.
The unchanged corrected acquisition remains available in **Details**. Defaults are
brightness **0** and contrast **20** (gain **1.3**). This darker preset is an
operator image preference, not a claim of improved matching accuracy. Existing
saved choices remain in effect. Recognition validation is tracked separately in
[processing validation](FINGERPRINT-PROCESSING-VALIDATION.md).
Brightness −100…100 maps to a −64…64 gray-level offset. Contrast 0…100 maps to a
white-anchored gain of 1…2.5. **Original** returns the exact acquisition pixels;
**Reset** restores the image-control defaults. **Invert** remains display-only.

Outside the NIST implementations, local ridge evidence is distinct from the
connected capture region. After freezing, the feature area fills enclosed holes
in the measured contact. A separate convex context envelope adds a bounded
32-pixel margin without authorizing exterior template features. Original pixels
use no artificial cut or inner guard. Optional image changes whiten excluded
background and use the configured guard. **Settings → Advanced capture policy**
contains optional local normalization and its strength, zero by default. Enabled
normalization with zero strength and neutral tone still preserves original pixels.
It does not sharpen, reconstruct ridges, synthesize features or resample geometry.
The separation of contrast, masks and inner-area filtering follows architectural
lessons from [SourceAFIS transparency](https://sourceafis.machinezoo.com/transparency/),
not its thresholds or matcher.

Editing a frozen, unaccepted capture clears obsolete results immediately and
coalesces native reprocessing after 150 milliseconds. Acceptance, comparison and
saving require the current results. **Adjust capture** is available immediately
after each capture, before **Accept finger**. It edits the retained original
without another scan. **Apply changes** returns the adjusted image to review;
acceptance remains a separate action. **Cancel** restores the capture as it was
when adjustment began. Adjusting the second finger preserves the accepted first.

The primary quality labels describe processing pixels. **Details** separates the
original scores and provides original, candidate-mask, ridge-support, retained-area
and processing views. Mask images use black foreground and a white excluded region;
coordinates start at the top left. Reference templates are never regenerated by
image adjustments.

Capture rules are validated policy settings, separate from sensor and record
geometry. **Settings → Advanced capture policy** exposes enhancement blend, the
quality-improvement margin and feature-edge guard. The current settings file
also retains contact evidence, empty-sensor confidence and presentation rules.
Unsafe settings are rejected. Native NIST algorithms and the PIV preparation path
are unchanged.

Select either finger slot to work on it, or review its accepted image. The export
keeps the first/second plan order even when the second finger is captured first.

**Clear person** clears the current label, identifier, accepted slots, capture and comparison reference,
and suggests a fresh two-finger plan, restores numeric metadata and automatic
exposure settings; application preferences remain unchanged. It leaves saved files intact.

An accepted capture remains available if the scanner is disconnected or its
session expires. Resuming preview rediscovers and reconnects the scanner.

## Save an unsigned template

The **Save options** popup starts with Vendor ID `0x0103`, **Vendor Unknown**,
as selected for the test workflow. Edit **Vendor ID**, **Product Type**, and
**Capture Equipment ID** when another value is appropriate. The fields accept
decimal values or hexadecimal values prefixed with `0x` and report invalid or
out-of-range input before writing.

A single-finger export uses its accepted capture. A two-finger record uses the
accepted first and second fingers in the displayed plan order.
Two-finger records require distinct fingers. Each view retains its own capture
time, acquisition settings, image, and quality result. Records use nominal
500 ppi geometry and contain the selected finger identities and minutiae.

The default destination on macOS is:

```text
~/Library/Application Support/OpenPhysical/Private Biometrics/Captures
```

Each save creates a separate private directory containing `fingerprints.fmr`,
`fingerprints.json`, and lossless original and processing PNGs for every view.
The current Studio package records measured selected contact (C), inferred enclosed
holes (H), the feature region (F), measured ridge support (S), feature uncertainty
(F minus S), and processing context (E) as distinct roles. Context references the
retained-mask file; a separate guarded-context plane records the actual processing
cut guard. The inner mask is the exact intersection of F with guarded context.
A wider context margin never authorizes additional template points. Reopen checks
that every saved point belongs to the eligible inner mask.
**Include processing diagnostics** in **Save options** adds the five distinct
contact-analysis planes: candidate, ridge support, accepted, rejected and inner.

The manifest binds the supplied person label, exact FMR bytes, image roles, source
acquisition and original capture-decision policy, raw and processed quality, current
processing policies and hashes, actual exposure,
selected revision and selection reason. Reopen verifies every advertised companion
before displaying processing pixels. Frozen image edits preserve the original
acquisition decision snapshot while recording their current processing policy.
The input role distinguishes a live sensor acquisition from a declared offline
image. Live packages require the recorded original whole-contact decision and its
complete policy and source associations. Offline packages explicitly record that
physical qualification is absent. Loading recorded metadata never creates a live
sensor authorization.
Studio accepts one current package format;
unsupported package markers or incomplete processing evidence fail with a precise
error. Standalone ANSI 378 and raw CBEFF imports remain independent of Studio
companions. The unsigned FMR contains no PIV CBEFF signature or card encryption.
The default destination persists across reboot outside the source checkout, with
owner-only directory and file permissions on macOS.

## Verify and visualize

Choose **File** to open an FMR or raw PIV CBEFF in **Compare**,
then select its reference finger. The utility shows
record metadata and encoded minutiae. If its associated manifest and source image
are present, it verifies their hashes and dimensions before overlaying minutiae
on that image. A standalone FMR can be inspected as a minutiae plot without an
image. Import does not reconstruct the original fingerprint image from minutiae.

The reference and current finger appear side by side. The current finger label may
be reassigned for comparison without changing the original capture or its saved
metadata. **Reference overlay** projects the reference points onto the captured
image using supported rotation, translation and scale registration. It never
substitutes extracted probe points. The alignment status reports unavailable
registration explicitly. Registration normalizes both records' pixel resolutions,
fits a bounded physical scale of 0.8–1.25, and requires unique geometric support
from at least eight points and 40% of the smaller template. Ending/bifurcation
labels remain visible but do not exclude cross-extractor correspondences.
An unavailable overlay does not change the independent Bozorth3 match decision.
Use **Capture now**
in the same workspace, then choose **Compare**. The **Details** popup contains the Bozorth3 score, configured
threshold, and match decision. Repeated comparisons reuse prepared templates.
The comparison policy identifier is `desktop-bozorth3-v1`; the initial threshold
is 40 and is editable.

Choose **Credential** to refresh the PC/SC reader list. Select the reader,
enter the card PIN in the masked field, and choose **Read card**.
The utility selects the PIV application, verifies that explicitly entered PIN once,
and reads its fingerprint object. The PIN field and temporary PIN buffer are
cleared after the attempt. PINs are never saved with capture packages. Removing the
credential, disconnecting its reader, or switching reference sources clears the
active reference before another comparison.

Each image has visible **+**, **−** and **Fit** buttons. New captures fit the
finger and its surrounding context, with a small margin, instead of fitting the
whole scanner frame. **Clear person** resets zoom and pan in both modes.
Minutia markers use a hollow circle and one detached line for an ending, or two
symbolic lines for a bifurcation. A soft white halo, black when inverted, keeps
them visible over ridges; the two lines do not claim measured branch directions. The image canvas also
supports keyboard navigation after receiving focus: **+ / -**
zoom, the **arrow keys** pan, and **Home** resets the view. A neutral focus boundary
shows which canvas receives these keys. Mouse scrolling and dragging provide the
same zoom and pan operations.

The expired TWIC reference retained for local testing is an unsigned FMR at:

```text
~/Library/Application Support/OpenPhysical/Private Biometrics/twic-expired-2026-11-10/piv-fingerprints-ansi378.fmr
```

It contains right-index and left-index views. Select the corresponding view when
comparing a live capture. Keep personal captures, reference records, and privacy
keys in their private storage directories rather than adding them to tests or Git.

## Development checks

Use the solution build, desktop tests, format checks, and size limits documented
in the repository guide. Deterministic tests use synthetic acquisition fixtures;
live validation uses an attended capture and retains its artifacts privately.
The attended hardware check covers scanner attachment, preview, selected-frame capture,
retake, one- and two-finger export, reopen, inspection overlays, comparison,
disconnect/reconnect, and app shutdown.

## Settings

The toolbar **Settings** popup edits the comparison threshold, automatic capture,
capture delay from 0 to 10 seconds, display inversion and advanced capture policy. Zero delay captures immediately.
Valid changes apply immediately; **Save** persists them in
`~/Library/Application Support/OpenPhysical/Biometrics/preferences.json`.
**Defaults** restores threshold 40, automatic capture on, a one-second delay, and inversion off.
Image-adjustment and policy choices persist alongside these preferences and survive **Clear person**.
Inversion changes displayed pixels only. Saved source images and templates retain
their original pixels.

## Desktop theme

The application uses [Material.Avalonia](https://github.com/AvaloniaCommunity/Material.Avalonia)
3.14.2 with Avalonia 11.3.6. The default light `CustomMaterialTheme` supplies the native Material
control templates, including button and navigation ripple feedback. The layout
uses native Material cards with subtle elevation, a compact type scale, rounded fields and
persistent action footers. All text uses its bundled Roboto family through
`MaterialDesignFonts`; controls have at least 48-DIP touch targets. The typography
follows [Material font guidance](https://m3.material.io/styles/typography/fonts). Optional identifier, scanner and save settings open in
focused popups, keeping the required person and finger plan visible. This package
implements Material 2 controls; the custom visual treatment does not claim a
Material 3 implementation. Shared styles live in `Views/DesktopTheme.axaml`; both
workspaces use the same canonical fingerprint canvas.
