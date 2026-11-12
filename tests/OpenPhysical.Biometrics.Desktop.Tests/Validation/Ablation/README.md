# Causal capture-pipeline ablations

This runner diagnoses image preparation and feature filtering. It does not change
application defaults or select parameters by maximizing a quality score.

The seven policies are fixed before scoring:

1. Exact raw pixels and the frozen historical whole-component mask.
2. Exact raw pixels and the current retained mask.
3. Exact raw pixels and the current guarded inner mask.
4. Mask whitening alone: automatic enabled, CLAHE blend 0, gain 1.
5. CLAHE alone: blend 0.5, gain 1, with mask whitening.
6. Gain alone: automatic disabled, gain 1.3, with mask whitening.
7. Current complete policy: CLAHE blend 0.5, gain 1.3, mask whitening.

All brightness offsets are zero. The raw variants reuse one real NFIQ 1
assessment, one real NFIQ 2 assessment and one extraction. The four processing
variants each assess their exact pixels once per quality engine and extract only
when eligible. The original assessment is passed through the production cache
association check. Operational failures remain typed failures.

Every successful representation uses the same canonical INCITS 378 view and
Bozorth matcher. The threshold is fixed at 40. All unordered genuine and
impostor pairs remain in the report, including unavailable representations.
Historical contact rejections are preserved in the raw whole-mask baseline.

The development cohort is the complete optical 500 ppi FVC2000 DB3_B set.
The previously examined FVC2002 DB1_B set is diagnostic only. The optical
500 ppi FVC2004 DB2_B set is reserved for separate final validation. This runner
rejects a manifest with role `final` before reading its image files.

Prepare the pinned public archive with `scripts/prepare-fvc-evaluation.py`, then
its historical masks with `scripts/prepare-fvc-baseline.py`. Both commands take
`--manifest tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/fvc2000-db3-b/manifest.json`.
Then supply both absolute paths explicitly:

```sh
rtk proxy env \
  OPENPHYSICAL_ABLATION_FOLDER=/private/tmp/openphysical-fvc2000-db3-b \
  OPENPHYSICAL_ABLATION_MANIFEST="$PWD/tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/fvc2000-db3-b/manifest.json" \
  dotnet test tests/OpenPhysical.Biometrics.Desktop.Tests -c Release --no-build --no-restore \
  --filter FullyQualifiedName~RunSevenPrespecifiedCausalAblationsWithoutReadingTheReservedFinalCohort
```

The JSON report records the complete pinned manifest, policy values and hashes,
raw and processing quality outcomes, extracted/filtered/exported feature counts,
mask areas, exact 0/255 clipping counts inside the feature mask, native call
counts and every pair score. It contains no image pixels or minutia coordinates.
No pass claim follows from this development report; any final improvement must
be evaluated separately on the reserved cohort after the policy is frozen.
