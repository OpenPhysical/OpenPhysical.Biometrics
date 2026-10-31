# Integrating biometric signatures

A PIV biometric signature covers the original 88-byte CBEFF header followed by the
biometric record. The detached signature block follows that content. Biometrics
prepares this boundary; an application-supplied provider performs cryptographic work.

Implement `IBiometricSignatureProvider.SignAsync` and
`IBiometricSignatureVerifier.VerifyAsync` in the application's CMS integration.
Requests own their bytes and carry the expected credential identity and
`BiometricSignatureRequirements`. Providers receive cancellation for external work.

## Issuance

Prepare a canonical fingerprint or facial record, encode it, and create a
`CbeffRecord` with its identity, validity, format, and acquisition metadata. Pass
that record, expected identity, and both providers to
`BiometricSignatureWorkflow.SignAsync`.

The signature-block length is part of the signed header. The workflow signs again
when the returned block has a different size, retaining the exact submitted bytes
for each attempt. It permits up to 32 attempts. A completed object has consistent
lengths, successful external integrity/envelope verification, and matching identity.

A signing result retains distinct trust and revocation findings. The issuer applies
its configured trust and issuance policy before passing encoded bytes to the card
transaction. [The issuance example](../examples/OpenPhysical.Biometrics.Examples/SignatureIntegration.cs)
shows this decision explicitly.

## Reading and verification

Parse the original CBEFF bytes and retain the resulting immutable record. Call
`VerifyAsync` with expected identity obtained from the application's card context.
The verifier receives the original signed header and record, together with the
associated detached block. Its authenticated identity attributes are checked
against both the expected identity and the header FASC-N.

Structural/profile inspection and external authentication findings remain distinct.
A reader application combines those findings with its card and access policy.
[The reading example](../examples/OpenPhysical.Biometrics.Examples/RecordReadingExample.cs)
shows the complete handoff.

## PIV-specific requirements

The request describes detached CMS SignedData version 3, one issuer-and-serial
signer, content type `2.16.840.1.101.3.6.2`, the message digest, and FASC-N,
signer-DN, and entryUUID attributes. The cryptographic integration selects keys,
algorithms, certificates, trust anchors, and revocation services.

These responsibilities follow [SP 800-76-2 sections 9.2 and 9.3](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-76-2.pdf).
