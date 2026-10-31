// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Piv;

internal static class RecordReadingExample
{
    internal static async Task<BiometricVerificationReport> VerifyAsync(
        ReadOnlyMemory<byte> cardObject,
        BiometricIdentity expectedIdentity,
        IBiometricSignatureVerifier verifier,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken
    )
    {
        // Card transport has already removed the card-object envelope. Parse the
        // original CBEFF bytes so verification uses exactly what the issuer signed.
        var parsed = CbeffCodec.Parse(cardObject.Span);
        if (parsed.Value is not { } record || !parsed.IsSuccess)
            throw new ArgumentException(
                "The card object needs structural review.",
                nameof(cardObject)
            );
        var inspection = BiometricInspector.Inspect(cardObject.Span, evaluationTime);
        if (!inspection.ProfileDiagnostics.IsEmpty)
            throw new InvalidOperationException("The card object needs profile review.");

        // The application supplies expected identity from its authenticated card
        // context. The provider returns integrity, envelope, trust, and revocation
        // findings; the application combines them into its authentication decision.
        return await BiometricSignatureWorkflow.VerifyAsync(
            record,
            expectedIdentity,
            verifier,
            cancellationToken
        );
    }
}
