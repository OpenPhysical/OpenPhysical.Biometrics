# Prepare a NuGet beta release

The initial coordinated beta uses these package versions:

- OpenPhysical.Biometrics components and CLI: **3.0.0-beta.1**.
- OpenPhysical.Biometrics.NBIS and its CLI: **0.1.0-beta.1**.
- OpenPhysical.Biometrics.NFIQ1 and NFIQ2: **0.1.0-beta.1**.

Versions follow Semantic Versioning. Each repository keeps its version in
`Directory.Build.props`; Biometrics pins the algorithm package versions there
and their source commits in its CI workflow. Changelogs record the dated beta
and retain an Unreleased section for subsequent work.

## Complete the gates

Use the exact commits intended for tagging. Each algorithm repository runs native
reference parity, memory-safety checks, managed tests, documentation/formatting
gates, and complete-package consumers across its five supported runtimes.
Biometrics runs its managed suites, examples, and isolated package checks on all
five runtimes, then aggregates complete packages and consumes those same packages
on all five platforms.

Release CLI packing requires complete NBIS and NFIQ1 runtime assets. Local smoke
checks use the explicit partial-runtime override for their current host.

After the final CI runs pass, create signed annotated tags on their tested commits:

- Biometrics: `v3.0.0-beta.1`.
- NBIS: `v0.1.0-beta.1`.
- NFIQ: `v0.1.0-beta.1`.

Verify the tag signatures and commit IDs before pushing the tags.

## Collect the publication files

Download **complete-beta-packages** from the successful Biometrics run. It
contains 11 Biometrics packages and three complete algorithm dependencies, with
source-feed folders retained for its external-consumer tests.

Download **nbis-packages** from the successful NBIS run and select the
`OpenPhysical.Biometrics.NBIS.Cli.0.1.0-beta.1.nupkg` file. The NFIQ run also retains
its independently validated packages in **nfiq-packages**.

For the publication bundle, keep one file per package ID/version: the 14 packages
from the Biometrics complete artifact plus the NBIS CLI, for **15 packages**.
Record the source commits, signed tags, successful run URLs, and SHA-256 of every
selected file. Preserve the complete packages and their notices together.

Publish algorithm and component dependencies before their consumers, following
the dependencies recorded in each `.nuspec`. Publish the root Biometrics package
and command-line tools after their libraries. The publisher supplies their own
NuGet account and publishing credentials.

## Validate a downloaded artifact

Restore the complete artifact's directory layout into sibling repository
checkouts and run:

```sh
python scripts/test-package.py --skip-pack --require-all-runtimes
```

This checks the actual ZIP contents and version graph, every algorithm's five
runtime assets, the CLI's embedded native assets, and fresh external consumers.
It also checks model hashes/notices, native fingerprint/quality operations,
facial processing, and installed command-line workflows.
