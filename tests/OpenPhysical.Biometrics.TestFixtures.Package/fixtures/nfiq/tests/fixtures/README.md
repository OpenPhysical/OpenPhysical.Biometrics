# Fingerprint quality fixtures

Twenty SD04 impressions cover ten finger pairs and all five pattern classes.
Craig I. Watson and NIST published these images in [NIST Special Database 4](https://www.nist.gov/publications/nist-special-database-4-nist-8-bit-gray-scale-images-fingerprint-image-groups).
The raw fixtures preserve every 512 by 512 grayscale pixel at the documented
nominal 500 ppi. `provenance.json` retains original PNG checksums, source history,
class/finger metadata and pixel conversion details from the verified SD04 import.
The original dataset terms remain in `NIST-DATA-NOTICE.txt`.

Five additional SFinGe images come from the NIST NFIQ2 2.3.0 release's `examples`
directory. `nfiq2-release-vectors.json` pins their original PGM and expected-output
hashes. Raw conversion removes only the PGM header; grayscale pixels stay exact.
The upstream NFIQ2 license and example attribution apply to these fixtures.

`nfiq1-reference.tsv` records original classifier status, class and full-precision
confidence for each SD04 impression. The official NBIS CLI outputs are retained
in `nfiq1-official-cli.json`; they independently agree with every recorded class.
`nfiq2-reference.tsv` records native status, score and upstream error code. These
include the original algorithm's defined feature-extraction failure outcomes.
`nfiq2-release.tsv` records dimensions and NIST's published example scores.

Build the independent original-source NFIQ2 tool with:

```sh
python scripts/build-reference.py --build-directory /tmp/nfiq-reference
```

Build official NBIS 5.0.0 separately with its setup/build instructions, then link
`tests/native/nfiq1_reference.c` against its original `nfiq`, `mindtct`, `mlp`,
`cblas` and `util` libraries. Reproduce the recorded outputs with:

```sh
python scripts/record-reference.py --nfiq1-tool /path/to/nbis/nfiq/bin/nfiq \
  --nfiq1-detail-tool /path/to/nfiq1_reference \
  --nfiq2-tool /tmp/nfiq-reference/build/bin/nfiq2_reference
python scripts/verify-fixtures.py
```

The NFIQ2 reference target compiles imported algorithm and dependency sources
without wrapper code or generated safety adaptations. Its independent scores
agree with all five expected values shipped in NIST's release. Native µnit tests
own exact algorithm parity; managed tests exercise binding contracts and workflows.

## Official NBIS NFIQ1 release vectors

Eight further grayscale captures come from the official NBIS 5.0.0 test archive.
`nfiq1-release-vectors.json` records their WSQ hashes, decoded pixel hashes,
original dimensions and published expected classes/confidences. The original
`nfiq_nist.log` is retained as `nfiq1-published.log`. Original `dwsq` decoding
preserves the pixel arrays used by NBIS; the 500 ppi value follows NBIS NFIQ's
explicit default for these release files with unspecified WSQ resolution.

Reproduce the import using the official test archive and separately built tools:

```sh
python scripts/import-nfiq1-release.py --archive /path/to/nbis_test_v5_0_0.zip \
  --nbis-directory /path/to/Rel_5.0.0 --detail-tool /path/to/nfiq1_reference
```

Build the detail tool with the original NBIS exported libraries:

```sh
cc tests/native/nfiq1_reference.c -I /path/to/Rel_5.0.0/exports/include \
  -L /path/to/Rel_5.0.0/exports/lib -lnfiq -lmindtct -lmlp -lcblas -lutil -lm \
  -o /path/to/nfiq1_reference
```

## Generated input-outcome fixtures

`negative/featureless.gray8` is a generated 128 by 128 white image at explicit
500 ppi. Original NFIQ reports its defined class 5 / TooFewMinutiae outcome.
`negative/truncated.gray8` contains only two bytes for the same dimensions and
exercises malformed-buffer rejection. These first-party fixtures contain no
personal capture data; their hashes are recorded in `negative/manifest.json`.
Native and managed tests use them for distinct quality and input-error paths.

`reference-provenance.json` records the complete original NBIS compiler flags,
setup/build commands and nfiq/dwsq executable hashes used for these results.
