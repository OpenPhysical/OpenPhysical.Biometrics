// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Piv;

// The issuer supplies its configured providers. Its key store and trust policy
// remain in that integration while Biometrics defines the exact content boundary.
internal static class SignatureIntegration
{
    internal static async Task<byte[]> IssueAsync(
        CbeffRecord prepared,
        BiometricIdentity expectedIdentity,
        IBiometricSignatureProvider signer,
        IBiometricSignatureVerifier verifier,
        CancellationToken cancellationToken
    )
    {
        var result = await BiometricSignatureWorkflow.SignAsync(
            prepared,
            expectedIdentity,
            signer,
            verifier,
            cancellationToken
        );
        if (result.Record is not { } completed)
            throw new InvalidOperationException(
                "The signing service returned an incomplete or inconsistent object."
            );

        // Integrity and identity binding were checked by the attachment workflow.
        // The issuer additionally requires its own trust/revocation results before
        // handing these bytes to the card-programming transaction.
        var findings = result.Verification!.SignatureFindings;
        if (
            findings.Trust != ValidationStatus.Passed
            || findings.Revocation != ValidationStatus.Passed
        )
            throw new InvalidOperationException("The issuer's trust policy rejected the result.");
        return CbeffCodec.Write(completed);
    }
}
