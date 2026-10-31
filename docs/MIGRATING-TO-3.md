# Migrating to version 3

Version 3 targets .NET 10 and gives biometric formats, processing, inspection, and
signature integration dedicated packages with standard .NET contracts.

## Package selection

Use `OpenPhysical.Biometrics` for the formats, PIV workflows, and fingerprint
entry point. Use `.Formats` and `.Piv` directly for a format/inspection integration.
Add `.Facial` for captured-pixel JPEG 2000 preparation, and `.Imaging` for owned
image buffers and transforms. The optional compatibility solution retains the
existing facial pipeline and ONNX integration while callers migrate.

## Contract changes

Canonical parsers return `ParseResult<T>` with a value, structural diagnostics,
and offset-aware inspection. Profile checks return typed findings. Processing
returns project-owned outcomes. External signature services use `Task<T>` and
cancellation. Input buffers are copied when retained; collections are immutable.

INCITS 378 fields use top-left pixel coordinates and two-degree angles. Resolution
is expressed in pixels per centimetre. Finger and view selection is explicit.
The compatibility minutia enum now uses `Other = 0`, `RidgeEnding = 1`, and
`Bifurcation = 2`, matching the wire format. Update persisted numeric enum values
and references to the former `OtherType2`/`OtherType3` names during migration.
Map stored application enum values by their original semantic meaning. Standardized
FMR files retain their existing wire bytes.
Convert through `record.ToMatchingTemplate(finger, viewNumber)` from
`OpenPhysical.Biometrics.Fingerprints` when working with NBIS templates, whose
native coordinate and angle conventions differ. This replaces NBIS's former
`Incits378FingerprintConverter` and `PivMinutiaeProfile` entry points. Parse once
through `Incits378Codec`, apply `PivBiometricProfile`, and reuse the canonical
record across inspection, issuance, and comparison. The command-line equivalent
is `biotool convert record.fmr reference.json --finger 2 --view 0`.

CBEFF headers preserve the exact original signed bytes. Date components use
unsigned decimal pairs. PIV fingerprint and facial format identifiers are
`001B/0201` and `001B/0501`, respectively. Signature providers receive the exact
header plus biometric record and the required identity attributes.

## Application integration

Replace middleware-local parsing with `.Formats` codecs and
`BiometricInspector`. Supply captures and enrollment metadata to the fingerprint
and facial preparation services. Connect the application's CMS and trust services
through the signature interfaces, then apply the application's authentication
policy to their separate findings.

Register fingerprint, facial, and ONNX capabilities explicitly. Fingerprint
processing uses its selected NBIS/NFIQ engines. The structured inspection report
feeds both CLI and middleware presentations.

Run `OpenPhysical.Biometrics.sln` for modern contracts and workflows, and
`OpenPhysical.Biometrics.Compatibility.sln` for the existing facial pipeline and
its compatibility tests. [The examples](../examples/README.md) show the data flow
and ownership at each step.
