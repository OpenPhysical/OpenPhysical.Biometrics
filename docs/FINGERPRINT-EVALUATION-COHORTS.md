# Evaluation cohorts

**Development:** FVC2000 DB3_B, all 80 impressions from 10 published finger IDs.
The [official documentation](http://bias.csr.unibo.it/fvc2000/databases.asp)
identifies an optical Identicator Technology DF-90 sensor, 448 × 478 samples,
and **500 ppi**. This cohort may be used for experiments and parameter selection.
Its archive SHA-256 is
`d6dfa420d004b71e64c9847204b665235b9e8eba84c8a837af4f577a4a60051f`.

**Reserved final:** FVC2004 DB2_B, all 80 impressions from 10 published finger IDs.
The [official documentation](http://bias.csr.unibo.it/fvc2004/databases.asp)
identifies an optical Digital Persona U.are.U 4000 sensor, 328 × 364 samples,
and **500 ppi**. Its archive SHA-256 is
`110c7baa1c429067fc363188c20c16366635edf965c7a67aac00419f893e8a58`.
Only source-file integrity, native Gray8 geometry, and duplicate detection are
checked before policy freeze. Quality assessments and matching scores remain
unexamined. The baseline generator refuses this role without a recorded policy
freeze; the development ablation runner also rejects the final role.

The different FVC2000 and FVC2004 collection events avoid the known volunteer
overlap between FVC2004 sensor groups. Public data do not provide person-ID
mapping across these events, so person-disjointness is **not verified**. The
FVC2004 protocol also collected four fingers per volunteer; ten finger IDs do
not imply ten people. No exact decoded images or centered 800 × 750 inputs are
shared between these cohorts or the previously examined FVC2002 DB1_B cohort.

Both archives are downloaded directly from the official public competition
sites. Images remain external inputs, not repository assets. Manifests under
`tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/` pin original member hashes,
losslessly decoded pixels, declared resolution, sensor, provenance URLs, and
unchanged centered input hashes. Centering adds white background without
resampling any source pixel. The existing FVC2002 DB1_B cohort is diagnostic data
after its first evaluation, not a fresh final holdout.

```sh
python3 scripts/prepare-fvc-evaluation.py /tmp/DB3_B.zip /tmp/fvc2000-db3-b \
  --manifest tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/fvc2000-db3-b/manifest.json
python3 scripts/prepare-fvc-baseline.py /tmp/fvc2000-db3-b \
  --manifest tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/fvc2000-db3-b/manifest.json
```

The historical baseline oracle is shared with the initial evaluation. Its sources
and GPL license are pinned in one compressed fixture, and are compiled only into
an isolated validation program. For development, the old analyzer rejects
`105_6` as insufficient ridge evidence; this is retained as an explicit acquisition
failure rather than silently removing that image from the cohort.

## Separate study with documented person IDs

The [official Neurotechnology sample download](https://www.neurotechnology.com/download.html#db)
provides a DigitalPersona U.are.U 4000 archive containing 520 TIFF images from
seven people, 65 fingers, and eight genuine scans per finger. Its small README
explicitly defines the three filename fields as person ID, finger ID, and scan
number. These are identity labels; an anatomical mapping for the finger numbers
is not supplied. Source imagery stays outside Git and is used for private local
validation. The archive has no corpus-specific redistribution license, and this
study makes no permissive-license or redistribution claim.

The official archive is
`https://www.neurotechnology.com/download/UareU_sample_DB.zip`, 59,671,809 bytes,
SHA-256 `fe141d8311853843405fabe6aed265e588366792a68913cad32dc92ba8f2043d`.
The README SHA-256 is
`df9d101664eb66a879d2d6a269beda074526e1412f9cb32e8d87fe72f69ed8a2`.
The master manifest pins all 521 archive members, including the README, and
individual container and decoded-pixel hashes. No exact decoded images are
duplicated within this archive or across its two partitions.

Every image is 326 by 357 pixels. All TIFF headers specify 500 pixels per inch
on both axes, with inch-based resolution units, agreeing with the sample
download page. The linked [hardware specifications](https://www.neurotechnology.com/fingerprint-scanner-digitalpersona-u-are-u-4000.html)
describe the optical reader as 512 ppi. The input claim is therefore the
**distributed nominal 500 ppi imagery**, not proven unaltered native sensor
output. Any vendor preprocessing is undocumented. Local decoding preserves
the samples without resampling: the TIFFs use eight-bit indices and an exact
256-entry identity grayscale palette, RGB(v, v, v), without a color conversion.

Before any image-header, pixel, quality, feature, matching, or visual inspection,
the seven documented person IDs were sorted and partitioned once:

- Development: `012`, `013`, `017`. All 30 fingers and 240 images remain together.
  There are 840 genuine and 27,840 non-mated unordered image pairs. The latter
  include 19,200 between-person and 8,640 same-person/different-finger pairs.
- Final: `022`, `027`, `057`, `076`. All 35 fingers and 280 images remain together.
  There are 980 genuine and 38,080 non-mated pairs: 28,800 between-person and
  9,280 same-person/different-finger pairs. Person `057` has five fingers; the
  other six people have ten each.

The pre-inspection split receipt is pinned by SHA-256
`4619631e0c7b5aa0a6bac601544a21468d24d438165974f02680381d2f560f47`.
All unordered pairs within each partition are retained. Genuine pairs require
both person and finger IDs to agree; all other pairs are non-mated. Decisions
use the unchanged inclusive threshold 40. Missing images or scores remain
explicit failures rather than reducing the declared denominators.

Manifests are under
`tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/neurotechnology-uareu/`:
`manifest.json` records the complete archive, while `development.manifest.json`
and `final.manifest.json` select only their respective people but retain the
complete archive member list. Metadata-only preparation verifies geometry,
palette, resolution, hashes, and exact duplicates. The original final manifest
was preserved unchanged when the chosen policy was frozen. Historical masks and
status fields were generated once into a separate derived manifest before the
single two-arm held-out evaluation. That evaluation failed the non-mated
acceptance gate; its four people are now diagnostic data for revisions, with
all attempts and unavailable endpoints preserved. See the
[failed result summary](validation/uareu-final-summary-20261111.json).
No final FVC2004 scores were opened by this study.

This establishes person separation **within this corpus**. Links to people in
previously examined FVC or SD04 datasets are unknown. Four final people and
dependent repeated impressions do not establish population-level accuracy.
Static images also do not validate live temporal burst selection. The prior
FVC studies and their results remain separate diagnostic evidence.

## Fresh UNSW 2D research cohort

SD302 is excluded from this work at the operator's request.

The [author deposit](https://zenodo.org/records/10432121) provides a separate
optical 2D archive, documented as 150 people, ten fingers per person and four
captures per finger. The supplied license permits research use and requires
preserving the license and publication citations. Raw images remain outside
Git. Acquisition documentation identifies the Crossmatch Verifier 300 LC2.0;
the documented configuration is 500 ppi and 640 × 480 pixels. All 6,000 BMPs
pass that geometry and uncompressed grayscale check. Their resolution fields
are 19,685 pixels/m on both axes, approximately 499.999 ppi.

Before downloading or inspecting the biometric archive, the person split was
fixed in `Fixtures/unsw-2d/preinspection-split.json` under the desktop test
project. Its SHA-256 is
`df2fa2cb52ce6e1de3458ec7ad9813de0c29d70dc0ee6be1d129d745601c2e48`.
The deterministic hash ordering assigns 30 people to development, 30 to final
validation and 90 to reserve. Each complete thirty-person partition contains
1,200 images, 1,800 genuine pairs and 717,600 non-mated pairs, comprising
696,000 between-person and 21,600 same-person/different-finger pairs. Missing or
duplicate samples must be recorded before quality or matching analysis;
participants cannot be replaced in response to scores.

The downloaded 614,529,117-byte archive matches published MD5
`39ce44a51b016e55904e9899918c11df`; its SHA-256 is
`6bae764887655e79f643d9aca925f79cd85274a011903a0031a3dd2ecb3b6560`.
No slots are missing. Lossless decoding finds 25 two-image exact duplicate
groups, all within one finger: 22 in development, one in final and two reserved.
None crosses people or partitions. Preserve every declared primary attempt;
report identical-sample genuine pairs separately without replacing people or
selecting exclusions from scores. The [integrity receipt](validation/unsw-2d-integrity-20261111.json)
pins all manifests, the inventory script and its original split.
Final-partition contact, quality, feature and matching analysis remains
prohibited until a new candidate policy and execution protocol are frozen.
Integrity-only decoding and duplicate checks have already occurred. The failed U.are.U final results do not serve
as fresh validation for that candidate. Person separation is documented within
UNSW; overlap with other collections is unknown.

`scripts/inventory-unsw-2d.py` verifies the downloaded archive and extracted
census, headers and pixels. It produces metadata-only manifests under ignored
research storage and refuses to overwrite them. Centering adds white padding
at offset (80, 135) on an 800 × 750 input, with no resampling. Contact, quality,
minutiae, matching and visual fingerprint inspection are not part of this step.

The deposit's license supplies these required research citations (its condition
mentions four references, but the supplied document lists three):

1. W. Zhou, J. Hu, S. Wang, I. Petersen and M. Bennamoun, “Performance evaluation
   of a large 3D fingerprint database,” *Electronics Letters*, 50(15),
   1060–1061, 2014. DOI: 10.1049/el.2014.1927.
2. W. Zhou, J. Hu, I. Petersen, S. Wang and M. Bennamoun, “Performance evaluation
   of 2D to 3D fingerprint recognition,” *CISP*, 1736–1741, 2013.
   DOI: 10.1109/CISP.2013.6743956.
3. W. Zhou, J. Hu, I. Petersen, S. Wang and M. Bennamoun, “A Benchmark 3D
   Fingerprint Database—Invited Paper,” *FSKD*, 2014.
