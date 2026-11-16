# Fingerprint processing validation

## Capture-admission limitation

Current admission retains connected contact C/F and component-level ridge checks.
Compatible reference or fixed-anchor evidence applies the existing full-W
static/unknown-exterior rule. Without that evidence, an original single-contact
observation may receive spatial-only capture admission, or exposure correction
only if too light. C/W photometric evidence stays explicitly unknown; no reference
or clean anchor is fabricated, and an established rejection cannot be escaped by
changing exposure. The proposed absolute periodic-ridge-extent hard
gate was rejected: it excluded previously qualified genuine public controls.
Its source-bound area measurements remain diagnostic only. A freshly changed
large smear with a qualifying local ridge patch can still receive processing
authorization. Coherent residue may also be admitted without a compatible
background comparison. This is not fingerprint-authenticity or liveness detection.

The fixed 16/32/48 extent, hull, and attachment-depth development experiments are
preserved under ignored `artifacts/developer-checks/hysteresis-boundary-experiment`.
None satisfied all prior-positive and adversarial controls, and none approved a
production rejection rule or matching policy. Earlier failed matching studies
and their thresholds remain unchanged.

## Replacement awaiting accuracy validation

The replacement separates measured connected contact, enclosed holes, feature
admission and image context. Feature admission is the selected contact with its
enclosed holes filled; the context envelope cannot authorize exterior features.
Live acquisition requires an exact source-bound runtime admission before NIST
processing; background comparison may be explicitly unavailable. Loaded
metadata does not recreate that runtime admission. Original pixels and their
original evidence remain available when editing a capture.

The accuracy study for this replacement has not run. A fresh UNSW person split was fixed before archive acquisition;
final quality and matching results must remain unopened until the replacement
policy and its execution protocol are frozen.

## Failed RawHull0 candidate

The previously selected candidate preserves the complete corrected acquisition pixels and
admits canonical features inside the connected support hull plus a 32-pixel
margin. Neutral settings use effective boundary guard zero. Dirty/light-contact
rejection and automatic capture remain active. NIST implementations are unchanged.
Image adjustments remain optional; the earlier gain/local-normalization default
is not being retained merely to preserve an application preference.

The candidate was chosen from development results before examining the final
cohort. Its subsequent prospective evaluation **failed**. It is not approved for
release; normal publication remains blocked.

## Frozen held-out result

All 280 images and 39,060 unordered pairs were retained at threshold 40. The
baseline accepted 859 of 980 genuine attempts and one of 38,080 non-mated
attempts. RawHull0 accepted 865 genuine attempts but three non-mated attempts,
so it fails the no-additional-nonmate criterion. Genuine changes were 13 gained
and seven lost; two non-mated accepts were gained and none lost. The baseline
had one explicit unavailable image; the candidate had none. No operational
errors occurred.

The full source, runner, binary, input and receipt identities remained frozen.
One assessment per quality engine and one extraction per image served both arms:
280 calls to each engine. See the bounded, feature-free
[failed result summary](validation/uareu-final-summary-20261111.json).

A six-image diagnostic replay reproduced all three non-mated acceptance scores
with six unchanged native extractions and zero new quality assessments. The
context margin admitted exterior uncertain points, while the support hull cut
off measured-contact features. No template reached the 128-point cap; pixels
and native quality results were identical. A context envelope must not silently
become feature authorization. This is a representation defect, not evidence for
a native-quality cutoff selected from the failures.

The failed study and its protocol remain immutable. This cohort is now examined
diagnostic data for any revised policy; a revision cannot be presented as a fresh
held-out success on the same cohort.

## Person-separated development result

The frozen U.are.U development partition contains three people, 30 fingers and
240 images. All images were available in all four arms. Every unordered pair
was retained: 840 genuine and 27,840 non-mated attempts. The inclusive Bozorth3
threshold remained 40.

| Arm | Genuine accepts | Non-mated accepts | Genuine gained / lost |
| --- | ---: | ---: | ---: |
| Complete original baseline | 747 | 0 | — |
| Original pixels, hull feature admission | 766 | 0 | 22 / 3 |
| Neutral background whitening, guard 0 | 754 | 0 | 22 / 15 |
| Neutral background whitening, guard 8 | 764 | 1 | 28 / 11 |

The last arm fails the development gate: its additional non-mated acceptance is
between different fingers of the same person. The chosen original-pixel arm
has the highest genuine acceptance count among the zero-additional-nonmate arms.
The criterion is net full-denominator acceptance; it does not claim that every
previously accepted genuine pair remains accepted.

All arms share original and whitened image work: 480 calls each to NFIQ 1,
NFIQ 2 and extraction cover the entire study. Guard variants reuse extraction;
their filtered matching representations are prepared once per image and reused
for all pairs. There were no operational errors or omitted attempts.

The bounded, feature-free evidence summary records ledger digests and exact
candidate parameters in
[`validation/uareu-development-summary-20261111.json`](validation/uareu-development-summary-20261111.json).
The full protocol, report and pair ledgers remain under ignored
`artifacts/evaluations/` with protocol digest
`19154a99420dbf49cb685e13a3f272e036e6b2314f8d289e37ec52b251f32ab7`.
A report-writing failure from the preceding execution is preserved separately;
it did not constitute an accuracy result or authorize publication.

See [cohort provenance and partitions](FINGERPRINT-EVALUATION-COHORTS.md) and the
[prospective full-attempt protocol](FINGERPRINT-RELEASE-PROTOCOL-V2.md).
Three development people and four held-out people cannot establish population-level performance; linkage
to earlier public FVC/SD04 participants is unknown.

## Historical failed default

The initial frozen FVC2002 DB1_B experiment failed. At threshold 40, the original
path accepted 230 of 280 genuine pairs and zero of 2,880 non-mated pairs. The
then-proposed enhancement path accepted 176 genuine pairs and zero non-mated
pairs: 55 previous genuine accepts were lost and one was gained. Two of 80
processing images were unassessable, leaving 157 pair scores explicitly
unavailable; the original gate failed rather than dropping those attempts.

The unchanged report is preserved at
[`validation/fvc2002-default-initial-20261110.json`](validation/fvc2002-default-initial-20261110.json).
Subsequent ablations examined pixel changes and feature admission separately;
this cohort is now diagnostic, not an untouched holdout. Its archived runner is
not the current application or release command.

## Remaining acceptance

Static single-image matching does not validate FS50 exposure control, temporal
best-frame selection, dirty-sensor rejection, UI latency or operator comfort.
Those require the separate performance and attended capture trials. Normal
publication remains blocked until actual final evidence passes
`check-desktop-policy.py`; a research receipt cannot approve release.
