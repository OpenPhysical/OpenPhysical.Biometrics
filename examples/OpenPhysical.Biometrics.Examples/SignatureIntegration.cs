// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Piv;

// The issuer supplies its configured providers. Its key store and trust policy
// remain in that integration while Biometrics defines the exact content boundary.
internal static class SignatureIntegration
{
    internal static async Task<Result<byte[]>> IssueAsync(
        CbeffRecord prepared,
        BiometricIdentity expectedIdentity,
        IBiometricSignatureProvider signer,
        IBiometricSignatureVerifier verifier,
        CancellationToken cancellationToken
    )
    {
        var signing = await BiometricSignatureWorkflow.SignAsync(
            prepared,
            expectedIdentity,
            signer,
            verifier,
            cancellationToken
        );
        if (signing.IsFailure)
            return Result.Failure<byte[]>(signing.Error.Message);
        var result = signing.Value;
        if (result.Record.HasNoValue || result.Verification.HasNoValue)
            return Result.Failure<byte[]>(
                "The signing service returned an incomplete or inconsistent object."
            );

        // Integrity and identity binding were checked by the attachment workflow.
        // The issuer additionally requires its own trust/revocation results before
        // handing these bytes to the card-programming transaction.
        var findings = result.Verification.Value.SignatureFindings;
        if (
            findings.Trust != ValidationStatus.Passed
            || findings.Revocation != ValidationStatus.Passed
        )
            return Result.Failure<byte[]>("The issuer's trust policy rejected the result.");
        return CbeffCodec.Write(result.Record.Value).MapError(error => error.Message);
    }
}
