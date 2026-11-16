# Build and release

## Build the shared library

This repository owns biometric libraries and packages. Enrollment Studio is built and released from its own repository. Shared workflow jobs consume immutable NuGet versions and do not check out or build NBIS/NFIQ sibling source.

```sh
dotnet restore OpenPhysical.Biometrics.sln --locked-mode
dotnet build OpenPhysical.Biometrics.sln -c Release --no-restore -m:1
dotnet test OpenPhysical.Biometrics.sln -c Release --no-build --no-restore -m:1
```

GitHub Actions requests `packages: read` and supplies `NuGetPackageSourceCredentials_openphysical` from its `GITHUB_TOKEN`. Required owner-managed access is: Gp4Net packages to Biometrics; Biometrics packages to Net, Enrollment Studio, Issuer Studio, Issuance Server, and Directory; Net packages to Enrollment Studio, Issuer Studio, Issuance Server, and Directory; Issuer packages to Issuance Server. Configure this in each private package or source repository's Actions access settings. These grants are not confirmed.

The isolated bootstrap feed at `/Users/mistial/Projects/OpenPhysical/studio-release-work/feed` supplies the current local development snapshots; `/Users/mistial/Projects/OpenPhysical/studio-release-work/package-build.config` points to it and NuGet.org. Do not overwrite or repack its existing artifacts. Biometrics packages are on version `.1` except `OpenPhysical.Biometrics.Packages`, for which `.3` is the imminent candidate. Algorithm packages are `OpenPhysical.Biometrics.NBIS` 1.0.1 and NFIQ 1.0.1.

## Frozen test fixtures

The three fingerprint test projects consume the local `OpenPhysical.Biometrics.TestFixtures` package. Its source snapshot preserves all 20 SD04 pixels, the existing fixture helper API, the NFIQ2 SFinGe image, manifests and license notices. `provenance.json` binds every included file hash to the pinned NBIS (`d8e67d...`), NFIQ (`fea2d...`) and NFIQ2 (`76b8c4...`) source commits. The package stays test-only and is not published to GitHub Packages.

Before restoring the solution locally, build the immutable local fixture package:

```sh
python3 scripts/verify-test-fixture-package.py
mkdir -p artifacts/test-feed
dotnet restore tests/OpenPhysical.Biometrics.TestFixtures.Package/OpenPhysical.Biometrics.TestFixtures.Package.csproj --locked-mode
dotnet pack tests/OpenPhysical.Biometrics.TestFixtures.Package/OpenPhysical.Biometrics.TestFixtures.Package.csproj -c Release --no-restore --output artifacts/test-feed
dotnet restore OpenPhysical.Biometrics.sln --locked-mode
```

The Biometrics workflow builds the package from the versioned source snapshot and then runs every solution test. It does not check out NBIS/NFIQ source repositories for fixtures.

The old `scripts/publish-desktop-macos.sh` path has been removed. Desktop packaging is owned by Enrollment Studio and Issuer Studio. This repository's current workflow covers its library solution only.

## Production releases

CI build/test results and development NuGet packages are not production qualification. Production release records must bind to the source commit and artifact manifest and show all blockers cleared. The four product repositories own their release workflows and operator instructions.
