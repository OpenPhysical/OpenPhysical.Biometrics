# Build and package checks

`ci.yml` builds and tests .NET 10 on Windows x64, Linux x64/ARM64, and macOS
x64/ARM64. It checks CSharpier formatting, proves public API documentation
failures, runs the modern and compatibility suites serially, captures managed
coverage, and exercises the paired NIST SD04 example plus explicit different-finger rejection workflows.

The workflow builds its pinned NBIS and NFIQ dependency checkouts into sibling
local package feeds. Their own workflows run independent reference parity,
µnit, allocation-failure checks, ASan, UBSan, and complete native-package tests.

Each Biometrics job packs the modern components and optional facial pipeline, consumes them in a temporary
project outside the checkout, installs the CLI, and uploads test coverage and
packages for review. Dependency versions and source commit pins are updated
together when adopting a new algorithm build.

Releases use Semantic Versioning and the maintained changelog. Release preparation
selects the reviewed package versions and native dependency packages from the
successful runtime matrix before signing a release tag.
