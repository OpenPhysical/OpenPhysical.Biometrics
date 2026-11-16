# Third-party fixture notices

## NIST Special Database 4

The SD04 raw grayscale images are lossless derivatives of the NIST distribution. They retain the original dimensions and pixels. The dataset citation, NIST terms, warranty disclaimer, and derivative notice are in `fixtures/nbis/tests/fixtures/sd04/README.md` and `fixtures/nbis/tests/fixtures/sd04/NIST-DATA-NOTICE.txt`.

## NFIQ 2.3 SFinGe example

The SFinGe grayscale fixture is from the NIST NFIQ 2.3.0 release examples. It is synthetic, not a person's capture. The NIST license is reproduced in `licenses/NFIQ2-LICENSE.md`; the original example attribution is recorded in `licenses/NFIQ2-EXAMPLES-README.md`.

## Included helper

`Sd04Fixtures.cs` is copied from OpenPhysical.Biometrics.NBIS at commit `d8e67d61af827d179beac51ddc4d933f75d4c695` and remains GPL-2.0-or-later. The package's `LICENSE` contains those terms. Its fixture bytes and helper source hashes are pinned in `provenance.json`.

Source repositories:

- NBIS test fixtures: [OpenPhysical.Biometrics.NBIS](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NBIS/tree/d8e67d61af827d179beac51ddc4d933f75d4c695/tests/fixtures)
- NFIQ test fixtures: [OpenPhysical.Biometrics.NFIQ](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NFIQ/tree/fea2d64e7e93388f460d57fc1219b44d5b48dd1f/tests/fixtures)
- NFIQ2 upstream example and license: [NIST NFIQ2](https://github.com/usnistgov/NFIQ2/tree/76b8c4e0b0541f3deab832b1a496e524edc0b5b6)
