# OpenPhysical.Biometrics.TestFixtures

This test-only NuGet package contains frozen, hash-identified NIST fingerprint test fixtures and a shared SD04 fixture helper. It is consumed only by the Biometrics NUnit projects. No runtime or product project may reference it.

The SD04 collection contains 20 fingerprint impressions from ten pairs. `fixtures/nbis/tests/fixtures/sd04/manifest.json` records the pairing, pixel hashes, source citation, transformation and reference data. The NFIQ2 SFinGe image is synthetic and preserves the published pixel bytes. Package provenance identifies the exact OpenPhysical source commits and the NIST NFIQ2 example commit used for each input.

The package is licensed under GPL-2.0-or-later for the included helper source. NIST fixture terms and the SFinGe example attribution are retained alongside the data. This is test material, not an operational accuracy set or a production qualification record.
