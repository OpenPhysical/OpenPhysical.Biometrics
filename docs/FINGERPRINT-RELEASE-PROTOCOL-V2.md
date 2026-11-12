# Prospective full-attempt matching protocol v2

This protocol was fixed before the person-separated U.are.U study. It implements
the requested acceptance criterion at threshold 40 while retaining explicit
image-quality rejections. The frozen RawHull0 held-out run subsequently failed:
non-mated accepts increased from one to three. Its freezes and complete ledger
remain preserved; no passing release record exists. See the
[failed result summary](validation/uareu-final-summary-20261111.json).
The rules below record the prospective study contract. This cohort is now
examined data for any revised policy.

The initial FVC2002 schema1 protocol required all 80 images to be assessable. Its
failed report, pair scores and stricter historical criterion remain unchanged.
This protocol does not reinterpret that failure. The original U.are.U
pre-inspection subject-split receipt also remains unchanged; this document
prospectively specifies how recorded inability contributes to attempted-pair
decisions rather than changing the split or omitting attempts.

## Frozen study inputs

Before final execution, freeze the production policy and parameters, complete
source and binary identities, dataset and historical-oracle hashes, immutable
subject partition, native engine identities and this protocol. The NIST
implementations remain unchanged. The final partition contains people
`022`, `027`, `057`, `076`: 35 fingers and 280 images. All unordered pairs are
retained:

- 980 genuine attempts, requiring both person and finger IDs to agree.
- 38,080 non-mated attempts: 28,800 between-person pairs and 9,280 pairs of
  different fingers from the same person.

The inclusive native Bozorth threshold is 40. All scoring and preprocessing
settings are fixed before final results are examined. Every source image stays
in its declared partition. No image or pair is removed because its assessment
or template is unavailable.

## Decisions and failure handling

A pair with two available templates retains its actual nonnegative integer
native score. Its decision is acceptance exactly when that score is at least 40.
If either endpoint is unavailable for an explicitly permitted image-related
reason, the score is JSON `null` and the attempt is a nonacceptance. A missing
score is never replaced with a numerical zero. Genuine unavailable attempts
therefore reduce genuine acceptance on the full denominator; non-mated
unavailable attempts remain nonacceptances on the full denominator.

Each unavailable image must identify the original image ID, a reason category
and an actual typed diagnostic code. The gate permits only these combinations:

- `ImageNotAssessable`: `QualityImageNotAssessable` or `Capture.Nfiq1NotAssessable`.
- `ContactRejected`: `Capture.ContactTooLight`, `Capture.UnclearContact`,
  `Capture.NoFinger` or `Capture.MultipleFingers`.
- `NoReliableArea`: `Capture.NoReliableArea`.
- `AmbiguousTissueRegion`: `capture.AmbiguousTissueRegion`.

Generic wrapper errors do not establish an expected image rejection. Preserve
the underlying cause. File/integrity failures, cancellation, provider failures,
crashes, invalid native results and unknown diagnostics abort evaluation; they
cannot be relabeled as image inability. No operational error is permitted in a
passing record.

## Schema2 release evidence

The report identifies `SchemaVersion: 2`,
`Protocol: "prospective-full-attempts-v2"`, `Threshold: 40`, `Passed`, and complete
`SourceHashes`. The source set must exactly match the gate's relevant acquisition,
processing, format and quality-provider source files, projects and dependency
locks. Adding, omitting or modifying a relevant file invalidates the evidence.

`ImageCount` and `Images[{Id, PersonId, FingerId}]` identify the whole cohort.
`Pairs[{LeftId, RightId, OriginalScore, ProcessedScore}]` contains exactly one
row for every unordered pair, without self-comparisons or duplicate/reversed
pairs. The gate derives categories and decisions from identities and actual
scores. It does not trust a reported passing flag or aggregate counts alone.

Top-level totals are `GenuinePairs`, `ImpostorPairs`, `BetweenPersonPairs` and
`WithinPersonDifferentFingerPairs`. `UnavailableOriginalImages` and
`UnavailableProcessedImages` must match each arm's explicit unavailable-image
list. `OperationalErrors` must be an empty array.

Both `Original` and `Processed` include:

- `UnavailableImages[{Id, Reason, DiagnosticCode}]`.
- `GenuineAccepted`, `ImpostorAccepted`, `BetweenPersonAccepted` and
  `WithinPersonDifferentFingerAccepted`.
- `Available<Category>Pairs` and `Unavailable<Category>Pairs` for `Genuine`,
  `Impostor`, `BetweenPerson` and `WithinPersonDifferentFinger`.

All counts are nonnegative integers and must equal the ledger-derived counts.
Available plus unavailable counts must reconcile with every declared category
and the complete image-pair total. Acceptance counts cannot exceed available
pairs. The baseline must contain a genuine acceptance and available non-mated
comparisons, preventing a vacuous zero-against-zero result.

The new study passes only when operational processing completes successfully,
all required evidence reconciles, processed genuine acceptance does not decrease
and processed non-mated acceptance does not increase relative to the baseline.
The comparison uses full attempted-pair denominators. Report gained and lost
genuine acceptances, availability and rejection causes, and both non-mated
categories separately. Availability-only rates are supplementary diagnostics.

## Publication and scope

Normal publication reads `docs/validation/live-image-policy-v2.json`. That file
does not exist until actual evaluation produces release evidence. The immutable
schema1 failure stays in `live-image-policy-v1.json`.

The candidate publisher invokes `check-desktop-policy.py --research-record OUTPUT`.
This writes a deterministic current-source receipt with `Passed: false`, the
protocol identifier and a research-only reason. It contains no invented image,
pair or score results and cannot authorize normal publication. The writer
refuses to overwrite the historical schema1 failure record. Bundles contain no
fingerprint images or minutia payloads as part of this release evidence.

Person separation is verified within this seven-person corpus; linkage to earlier
FVC/SD04 participants is unknown. Four final people and dependent repeated
impressions do not establish population-level accuracy. Static image matching
does not validate temporal FS50 burst selection, sensor behavior, UI latency or
the separate performance and live-operator requirements.
