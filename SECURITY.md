# Security policy

## Supported versions

Security fixes target `master`. Reports should identify the exact commit or
package version and the selected NBIS, NFIQ, image-codec, or ONNX dependencies.
Versioned support commitments are recorded here as releases are adopted.

## Report a vulnerability privately

Email [security@mistial.dev](mailto:security@mistial.dev) with a subject such as
`[OpenPhysical.Biometrics Security] Brief description`.

Start with a concise description and a reproduction using synthetic data or the
public NIST fixtures. Include the affected API or command, operating system,
process architecture, .NET version, dependency versions, and relevant redacted
diagnostics. Native status codes and sanitizer findings help identify issues in
the separate algorithm bindings. Include suggested remediation and your credit
preference when available.

Keep live captures, minutiae, card dumps, personal identity data, credentials,
and private keys in your protected environment. Arrange an encrypted channel
with the response team before sharing additional sensitive material. Review
crash dumps and sanitizer reports for retained buffers and local paths before
sending them. Coordinate public disclosure with the response team.

Vulnerability reports can be submitted directly. Code patches follow the
[contribution guide](CONTRIBUTING.md).

## Response and coordination

Our response targets follow the OpenPhysical reporting policy:

- Acknowledge reports within five business days.
- Provide an initial assessment or status update within ten business days.
- Provide an update at least every fourteen days while an accepted report is open.

We investigate impact, reproduce the issue, coordinate dependency fixes when
needed, and agree on remediation and disclosure with the reporter. Advisories,
CVE coordination, and reporter credit follow the confirmed findings and the
reporter's preferences.

## Scope

This policy covers the biometric library, optional processing packages, and CLI:

- Canonical CBEFF, INCITS 378, and INCITS 385 parsing and writing, including
  bounds, lengths, extensions, profile checks, and buffer ownership.
- Fingerprint enrollment, quality adaptation, selected-view conversion,
  comparison policy, and integration with the NBIS and NFIQ packages.
- Exact signed-content preparation, signature-block attachment, external
  verification handoffs, and identity binding.
- Structured inspection and control of sensitive output and operational logs.
- Facial image preparation, shared image buffers, optional model inference,
  dependency loading, package contents, and distribution integrity.

The independently maintained [NBIS](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NBIS/blob/master/SECURITY.md)
and [NFIQ](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NFIQ/blob/master/SECURITY.md)
policies describe their native algorithm, ABI, model, and runtime scope.
