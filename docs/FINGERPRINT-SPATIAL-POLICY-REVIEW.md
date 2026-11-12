# Spatial confidence proposal review

This is a review of a proposed application policy, not a validation result or a
NIST algorithm change. FVC2000 DB3_B and the previously examined FVC2002 DB1_B may
inform development. FVC2004 DB2_B remains reserved for final evaluation.

## Keep native measurements intact

The proposed supported / uncertain / rejected distinction belongs outside NBIS.
Preserve every retained minutia's actual coordinates, type, direction, and native
quality. Store custom spatial evidence under its own versioned policy; do not
write it as NFIQ quality or replace native per-minutia reliability.

The unmodified NBIS source independently confirms why a universal quality floor
is inappropriate: `mindtct/src/lib/mindtct/quality.c`, `gen_quality_map` lines
107–193, lowers reliability in high-curvature or low-flow neighborhoods.
`combined_minutia_quality`, lines 218–298, maps quality C to 10–24% and D to 5–9%.
A quality≥25 or ≥50 rule would therefore remove these native features without
establishing that their anatomy is false. Quality remains useful ranking evidence.

SourceAFIS provides relevant concepts, not transferred constants or validation:
[relative contrast masking](https://sourceafis.machinezoo.com/transparency/relative-contrast-mask)
separates local usability from other image areas;
[inner masking](https://sourceafis.machinezoo.com/transparency/inner-mask)
provides a border guard;
[top-minutiae selection](https://sourceafis.machinezoo.com/transparency/top-minutiae)
limits crowded or lower-value features after extraction. These do not establish
that coherence alone identifies valid fingerprint anatomy, or that a particular
erosion or native-quality threshold is universally safe.

## Resolve before freezing the candidate

Use direct measured ridge support S, not expanded retention area, for any
coverage fraction within the admitted original texture component C. If C is
retained, C outside S remains **uncertain**, not newly measured support. Keep the
original support plane unchanged in diagnostics.

The proposed majority coverage and 32-pixel exterior-distance conditions are
development choices. Declare their strict boundary behavior, distance norm,
connectivity, units, and configurable validated bounds. Exterior-only distance
does not constrain a large enclosed unsupported island. Bound or explicitly
classify those holes while preserving legitimate low-coherence core/delta areas;
enclosure alone is not positive evidence.

An artificial-boundary guard must refer to a boundary actually introduced by
pixel exclusion or whitening. An unchanged raw image has no newly created mask
edge. A direction-aware continuation rule would require its own coordinate and
boundary tests, without altering the underlying native features.

Useful independent geometric tests include an enclosed curved core, a large
unsupported internal island, a narrow unsupported bridge, a long tail, a small
separate component, and the exact coverage/distance boundary. They should assert
both preserved legitimate regions and excluded unsupported regions.

## Prevent validation leakage

Freeze the complete candidate implementation and all parameter values before
examining final quality or matching outputs. Record the policy/code versions,
NIST provider identities, image transformations, canonical 128-point selection,
threshold40, and exact input-manifest hashes. Do not search image parameters or
adjust thresholds against the final results.

Keep original pair attempts, available scores, acquisition/image rejections, and
operational failures separate. No input may disappear from the denominators.
Changing a policy after the final run makes that cohort examined development
data and requires a different final holdout. Public finger IDs are not verified
person IDs; cross-event person-disjointness remains unknown.

## Follow-up measured geometry

The fixed majority-plus-distance promotion candidate was rejected during design:
it qualifies 0 of 80 development images and 33 of 80 examined diagnostic images.
It is not a universal tissue rule. Retention itself is fragmented in 20 of 80
development images and 4 of 80 diagnostic images under the v3 policy.

The unchanged-pixel ablations also oppose a universal mask choice. At threshold40,
whole-component / retained / inner filtering yields 75 / 133 / 138 genuine accepts
in development FVC2000 and 230 / 208 / 186 in examined FVC2002. Each cohort has280
genuine attempts; available scores and missing attempts remain separately recorded
in their reports. These are design observations, not final validation.

The revised candidate starts from the ridge-neighborhood envelope, applies a
declared contour closing, and distinguishes contiguous tissue from local support.
It still requires exact anchor, reach, hole, connectivity, and rejection rules
before freezing. A supported ring around arbitrary noise cannot be distinguished
from a weak core through topology alone. Additional source/session evidence or
an explicit ambiguous-segmentation rejection is needed; enclosure is not proof.

## Connected original-pixel candidate

The fixed connected-tissue candidate preserves original pixels and applies no
artificial cut guard. It improves development genuine acceptances from 75 to
111 of 280 attempts, but only 66 of 80 images qualify. The examined diagnostic
cohort falls from 230 to 213 of 280 genuine acceptances, with all 80 images
available. Both cohorts retain zero observed impostor acceptances. These results
fail the nondegradation requirement; this candidate is not an application default
and does not authorize final-cohort evaluation. The frozen reports are
`FVC2000_DB3_B-connected-original-v1-eb05bc458da3.json` and
`FVC2002_DB1_B-diagnostic-connected-original-v1-75ed658c40df.json` under the ignored
`artifacts/evaluations` directory.

Live preview now retains the complete image. Brightness and contrast can update
live, while final segmentation and local enhancement run after publication stops
and acquisition drains. Tissue connectivity alone does not establish that
filtering its exterior preserves recognition utility.

The connected-original experiment shares the exact extracted native template and
unchanged pixels with its raw control. In the diagnostic cohort, replacing the
whole-contact feature admission mask removes a net 261 exported points and loses
17 genuine acceptances, with none gained. This isolates point exclusion rather
than image contrast or coordinate conversion. A no-native causal derivation that
uses tissue only for eligibility restores 230 diagnostic acceptances by
construction, but loses 15 development acceptances through additional unavailable
images. Neither result justifies making tissue qualification universal.

The next prespecified pixel ablation keeps ordinary contact eligibility and the
whole-contact feature mask fixed, and varies only whitening outside that mask
and a fixed mild contrast gain. A separate display contour must preserve every
source contact pixel and cannot authorize feature deletion.

## Whole-contact pixel control

With historical contact admission held fixed, whitening only outside that contact
improves genuine acceptances from 75 to 84 (FVC2000 development), 230 to 232
(FVC2002 examined diagnostic), and 747 to 756 (person-coded U.are.U development).
No observed non-mated acceptances are added. Contrast slider 7 (gain 1.105) yields
73, 230 and 758 respectively, so even this mild fixed gain does not satisfy
nondegradation across the examined design sets. These are causal pixel controls,
not validation of a replacement production contact mask. They use the exact
historical C plane; the current v3 selected contact must be evaluated separately.

Native point diagnostics further oppose a universal reliability floor. The
connected mask removes 294 raw diagnostic points and adds 33 (net loss 261). Only
76 removed points satisfy both native quality at least 25 and distance at most
16 pixels from that mask. A point-centered local ridge window admits only two; an
inward-shifted probe has not been measured. High native reliability and local
periodicity are distinct evidence, not anatomical proof. A proposed exterior
grader remains outside the application and has not been validated.

A simpler connected-envelope hypothesis is under review: the convex hull of all
selected measured support, expanded by a fixed 32-pixel Chebyshev margin. It
retains weak enclosed tissue without promoting an entire connected smear from one
small ridge patch. The margin and an 8-pixel modified-boundary guard are explicit
application policy, not NIST requirements. It must preserve every support island,
never synthesize pixels or alter native qualities, and pass geometric, negative
contact and recognition checks before adoption.
