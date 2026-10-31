# Contributing to OpenPhysical.Biometrics

Focused contributions to biometric records, PIV profiles, inspection, processing,
.NET APIs, interoperability, tests, documentation, and packaging are welcome.

## Policy and contributor rights

Participation follows the [Bounded Contribution Policy](CODE_OF_CONDUCT.md).
Code and documentation contributions require acceptance of the
[Contributor Copyright Assignment](CONTRIBUTOR_ASSIGNMENT.md) before acceptance.
It preserves commercial licensing while promising that accepted contributions
remain available under the project's `GPL-2.0-or-later` license. Contributors retain
the agreement's broad license to reuse their own contributions.

If an employer or organization owns the work, an authorized representative must
complete the entity assignment. Disclose third-party code, generated material,
standards excerpts, data, and their licenses. Do not submit material without the
rights required by its applicable terms.

No repository CLA service has been configured by this change. Until one is in place,
arrange a separately signed copy through [opensource@mistial.dev](mailto:opensource@mistial.dev).
Do not post personal signature records in public issues or pull requests.

## Development

```sh
git clone https://github.com/OpenPhysical/OpenPhysical.Biometrics.git
cd OpenPhysical.Biometrics
dotnet tool restore
python scripts/build-development-dependencies.py
dotnet csharpier format .
dotnet csharpier check .
dotnet build OpenPhysical.Biometrics.sln -c Release
dotnet test OpenPhysical.Biometrics.sln -c Release
python scripts/verify-documentation-gate.py
```

Use .NET 10, Python 3, CMake, and a supported C11 compiler. Follow the [architecture guide](docs/ARCHITECTURE.md)
and [fingerprint workflow guide](docs/FINGERPRINT-WORKFLOWS.md). Run .NET builds and tests serially.
Every project references CSharpier.MsBuild 1.3.0. Debug builds format that
project, and Release builds fail when it is unformatted. The CLI commands above
also cover repository-root project XML.
Write public XML documentation with API changes; all warnings are errors.

Use NUnit for managed behavior and interop. The separate NBIS and NFIQ repositories
own native algorithm parity and safety through µnit, ASan, and UBSan. Keep
algorithm oracle tests with those bindings and test composition here. Preserve
pinned dependency versions and their notices. Never submit local
card dumps, private fingerprint samples, credentials, or keys.

## Versioning and compatibility

Use [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).
The library and CLI ship together with the same `MAJOR.MINOR.PATCH` version;
algorithm packages retain their independent versions and provenance.

From 1.0 onward, incompatible changes require a major release, compatible
features or deprecations require a minor release, and compatible fixes require
a patch release. Before 1.0, breaking changes require a minor increment;
patch releases remain compatible. Mark previews with a prerelease suffix such
as `-rc.1`. Never replace an already published version's contents.

The compatibility contract includes documented public .NET APIs, CLI arguments and output, template JSON, and supported runtime requirements.
Changing documented matching behavior requires a compatibility review and a
changelog note describing any need to reevaluate acceptance thresholds. ABI and
JSON schema identifiers are separate from package versions and must be updated
when their own contracts change.

Describe compatibility impacts under Unreleased as work is done. At release,
choose the appropriate version, update the shared version in
`Directory.Build.props`, and move those entries to a dated changelog section.

## Pull requests

Branch from `master`, use signed commits, and open a focused pull request. Before review:

1. Run formatting, the Release build, relevant tests, and documentation gates.
2. Update user-facing/API documentation with behavioral changes.
   Add a concise entry under Unreleased in [CHANGELOG.md](CHANGELOG.md) for
   user-visible changes, compatibility changes, and fixes. Record upstream
   adaptations in the provenance document as well.
3. Identify third-party or generated material and retain its notices.
4. Provide contributor-rights coverage for every human commit author, through a
   configured CLA service or an accepted signed agreement.
5. Complete the pull request checklist and state any untested platform/hardware boundary.

Changes, including maintainer changes, should pass review and CI with valid signed
commits and contributor-rights coverage. Acceptance is at maintainer discretion.
Commercial inquiries and contribution-rights questions go to
[opensource@mistial.dev](mailto:opensource@mistial.dev). For vulnerabilities, follow [SECURITY.md](SECURITY.md) and report privately to
[security@mistial.dev](mailto:security@mistial.dev); do not use public issues.
