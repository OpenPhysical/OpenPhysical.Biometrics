# Face and fingerprint enrollment

Studio keeps one identity and a selected capture plan. The default plan requires
one face and two different fingers, suggested from opposite hands. Face-only and
fingerprint-only plans are available under Required captures. Selecting a modality never
accepts its pending capture. Retaking one item preserves other accepted items.

This is a real-person capture workflow in **OpenPhysical Biometric Studio**. It exports unsigned encrypted staging for the separate target **OpenPhysical Issuance Studio**, currently Net's Signing application. Capture does not issue certificates or administer a card. The [hosted plan](../../OpenPhysical.Net/docs/planning/hosted-test-issuance.md) places generated TEST faces and `NotEnrolled` fingerprints in an explicit future issuer profile; neither is an alternative acquisition result in this workflow.

## Capture and review a face

1. Enter a name or local label and choose the Face capture section.
2. Select an available camera and one of its actual native modes. The provider
   prefers 1920×1080 at 30 frames/second when supported. Built-in and USB sources
   use the same interface. Unsupported pixel formats and inactive sources produce
   a specific error; they are not silently color-converted or substituted.
3. Face detection supplies guidance while preview is live. **Auto capture** is on
   by default and uses the configured capture delay. **Capture face** remains
   available for manual triggering. Both freeze the acknowledged image and stop
   the camera. Live mirroring is display-only; saved portraits are unmirrored.
4. Automatic detection supplies all crop geometry. Operators never place, drag or
   enter head-boundary points. An unusable detection requires a clear retake
   instruction, not a marking task. Estimated geometry remains identified as such
   in capture evidence.
5. Review the proposed portrait, confirm the portrait review checkbox, then choose
   **Accept face**. A valid detected crop must not require an adjustment step.
   Retake preserves other accepted captures.
6. Acceptance advances to the next incomplete modality. Save person requires all
   selected captures to be accepted.

Detection does not prove Full Frontal conformance. SP 800-76-2 §7.2, Table 12
requires reviewed acquisition and image properties. The shared Facial and Piv
libraries check source-supported cropping, sRGB, dimensions, eye placement, head
geometry, image type and compression. The application default is 480×640; it is
not a mandated PIV size. It does not upscale acquisition pixels. Facial record
quality remains the standard unreported value. Sharpness and backdrop evidence
are advisory measurements, not compliance scores or percentages.

## Correction, rejection and attended review

Automatic preparation normalizes source orientation and color to sRGB, then fits
a source-supported crop with uniform scaling and no upsampling. It may level a
small allowed source roll. It encodes the accepted pixels within the PIV
compression limit; it does not repair blurred detail, reconstruct a turned face,
or cosmetically alter skin tones to obtain a passing result.

Capture processing rejects missing or multiple faces, excessive assessed source
pose, unusable pose geometry, insufficient source coverage, impossible framing,
invalid image or record metadata, and encoding/compression failures. Source pose
is assessed before crop leveling. The PIV pose limit is strictly less than five
degrees in each of pitch, yaw and roll; estimated angles do not establish
physical camera qualification.

The mouth-closure check reuses the captured landmarks. Its default allowed
inner-lip gap is four percent of mouth width, an adjustable application tolerance
rather than a standard's numeric limit. A failed check retains the real cropped
portrait for inspection. Eligible automatic failures require a retake or an
explicit override recorded with the unsigned capture; missing pixels or invalid
record structure cannot be overridden. See the [mouth policy validation notes](validation/mouth-closure-policy.md).

The operator must still confirm focus from nose to ears and chin to crown,
visible skin gradations without exposure clipping, balanced lighting, natural
color, neutral expression, visible eyes, permitted obstructions and a suitable
background. Missing confirmation prevents acceptance. Correct unsuitable
illumination, camera positioning or camera settings and retake the photograph.
A raw sharpness or brightness measurement alone does not approve the image.

## Applicable standards

The issued-object target is SP 800-76-2 section 7.2/Table 12 and its INCITS
385-2004 Full Frontal profile. INCITS clauses 7.2.2, 7.3.2-7.3.6 and 8.3 define
pose, photographic quality and framing. PIV requires sRGB and limits compression
under Table 12 notes 6 and 8. Optical evidence remains recommended in Studio's
unsigned staging workflow as described below.

ISO/IEC 19794-5:2011 has different requirements, including an eight-degree roll
limit and different head-size ranges. Those values do not replace the PIV
profile. Its numeric lighting rule likewise does not become a PIV threshold.
Neither a vendor screenshot's quality percentages nor unvalidated sharpness,
skin-brightness or clipping cutoffs are treated as normative PIV checks. The
INCITS seven-bit facial dynamic-range wording is a recommendation.

The token ROI illustration is informative compression guidance, not a required
full-frontal crop. The corresponding local INCITS 385 edition places it in
Annex C.5-C.6/Figure C.4; numbering differs between editions. Guides do not change
the record's Full Frontal image type or any acceptance criterion.

## Recommended camera setup

**Settings → Camera setup** records explicit acquisition evidence for the exact
camera and native mode. At a distance of 1.5 m, a 20 cm target must span at least
240 original-image pixels. Record the measured width, operator identifier and
physical setup confirmation. Nominal camera resolution alone cannot establish
this property. Recheck after changing optics or zoom. Setup is recommended and
never blocks Studio capture, acceptance or saving. Unknown evidence stays false
with an `OpticalResolutionUnknown` finding in the encrypted package. The later
issuer can apply its own acquisition policy.

The measurements contain no biometric image or landmarks and are written with
owner-only permissions. Per-photo review remains required even with a saved
camera measurement. A measurement for another device or mode is not transferable.

## Image guides

**Remove background** appears below a captured photo and is on by default.
It also appears in Settings as **Replace photo background**. Studio separates
the person from the background and uses the median RGB color of the removed
original-image pixels as a uniform replacement.
It preserves the original camera image in memory. Changing this setting reuses
the frozen capture and its cached mask; it does not take another photograph.
Review the resulting portrait before accepting. The accepted facial record uses
the displayed processing image, and encrypted metadata records the replacement
policy, model, mask and image identities. Segmentation is not a compliance score.

**PIV guides** appears after capture and is on by default. The live preview stays
clear of PIV framing guides. Thin red lines show the portrait frame, dimension
arrows, centerline, permitted eye-height band and crosshairs at the detected eyes.
The diagram has no numeric labels. Eye positions come from the frozen Google
analysis rather than fixed positions from a sample photograph.

The quiet green rectangle is a proportional **suggested ROI** from an informative
token-image example. It is a visual aid on the full-frontal portrait; it neither
crops the image nor establishes an acceptance requirement. The token example's
sample dimensions and eye placement do not become full-frontal requirements.

Framing guides share the automatic crop/profile geometry. Guides never change
pixels, encoded dimensions, accepted bytes or inference results. They describe
image framing rather than station certification or optical proof.

**Face detection** independently shows the cached Google mesh and detection
extent. It is off by default. Both toggles leave accepted record bytes unchanged.
There are no point-placement hints, coordinate fields or manual marking controls.

## One encrypted handoff

Sections prepare immutable standard records. The central writer combines them
with association, provenance, reviewed acquisition evidence and encoded sizes in
one encrypted `.opbio` file. Face storage includes only the accepted portrait in
`face.bin`, not the uncropped camera frame or landmarks. Fingerprint companions
retain their exact original and processing images inside encryption.

**Open person** authenticates the whole package before inspecting its records and
verified companions. It neither regenerates features nor reruns facial analysis.
This read-only view is separate from the current enrollment session.

The uncropped camera frame stays in RAM until Clear person or shutdown. Leaving
the Face workspace, explicit package lock, OS lock, Clear person and shutdown stop
camera acquisition. Disconnect pauses preview, preserves completed captures, and
requires explicit reconnection. Clear person clears both modalities and comparison
references while retaining preferences and the unlocked package passphrase. Lock
and exit forget the passphrase; saved ciphertext is left intact.

The package is unsigned issuance staging. The separate issuer must construct signed
CBEFF, supply issuance identity and check actual card capacity. Studio does not
create dummy FASC-N values, signatures or completed PIV objects.

## Delivery evidence

The macOS backend uses a first-party ARC-managed AVFoundation bridge. Read-only
catalog testing confirmed that enumeration preserves device formats. The bundled
app launched through LaunchServices, showed an upright built-in 1920×1080/30
preview with a populated mode selector, and completed an attended capture without
the earlier FlashCap corruption/status defect. Native mode-switch stress and the
complete private face-and-two-finger package trial remain acceptance activities.
Physical external USB webcam testing was deferred at the operator's request.
Public-fixture workflow and format tests complement attended hardware trials.
