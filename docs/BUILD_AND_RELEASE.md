# Build and release

## Build the shared library

This repository owns biometric libraries and packages. Enrollment Studio is built and released from its own repository. Shared workflow jobs consume immutable NuGet versions and do not check out or build NBIS/NFIQ sibling source.

```sh
dotnet restore OpenPhysical.Biometrics.sln --locked-mode
dotnet build OpenPhysical.Biometrics.sln -c Release --no-restore -m:1
dotnet test OpenPhysical.Biometrics.sln -c Release --no-build --no-restore -m:1
```

GitHub Actions requests `packages: read` and supplies `NuGetPackageSourceCredentials_openphysical` from its `GITHUB_TOKEN`. Access to this repository's private fixture package was verified by its publication workflow. Runtime packages need explicit Actions access grants for their consuming repositories; cross-product grants remain unconfirmed. The [shared release specification](https://github.com/OpenPhysical/OpenPhysical.Net/blob/master/docs/architecture/studio-products.md) owns the cross-product dependency and access requirements.

Local developers need a GitHub Packages credential with `read:packages`, supplied through the `NuGetPackageSourceCredentials_openphysical` environment variable or their NuGet credential provider. Keep credentials outside the repository. NBIS, NFIQ1 and NFIQ2 version `1.0.1` use exact package-ID mappings to NuGet.org; the private fixture package uses GitHub Packages. Every project retains a dependency lock file.

## Frozen test fixtures

The three fingerprint test projects consume private `OpenPhysical.Biometrics.TestFixtures` version `1.0.0-dev.20261116.1`. Its source snapshot preserves all 20 SD04 pixels, the existing fixture helper API, the NFIQ2 SFinGe image, manifests and license notices. `provenance.json` binds every included file hash to the pinned NBIS (`d8e67d...`), NFIQ (`fea2d...`) and NFIQ2 (`76b8c4...`) source commits. The package remains test-only.

With package access configured, verify source provenance and restore the published archive:

```sh
python3 scripts/verify-test-fixture-package.py
dotnet restore OpenPhysical.Biometrics.sln --locked-mode
```

CI restores this same archive and runs every solution test. It does not rebuild a local fixture feed. The separate fixture publication workflow binds each new version to a clean signed source checkpoint, retains the archive manifest, and verifies private visibility, repository linkage and exact bytes through a fresh authenticated restore. See [package publishing](PACKAGE_PUBLISHING.md) for the accepted publication record.

The old `scripts/publish-desktop-macos.sh` path has been removed. Desktop packaging is owned by Enrollment Studio and Issuer Studio. This repository's current workflow covers its library solution only.

## Production releases

CI build/test results and development NuGet packages are not production qualification. Production release records must bind to the source commit and artifact manifest and show all blockers cleared. The four product repositories own their release workflows and operator instructions.
