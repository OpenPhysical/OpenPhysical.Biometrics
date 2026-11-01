// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Piv;

internal static class RecordReadingExample
{
    internal static async Task<Result<BiometricVerificationReport>> VerifyAsync(
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
        if (parsed.IsFailure)
            return Result.Failure<BiometricVerificationReport>(
                "The card object needs structural review."
            );
        var inspection = BiometricInspector.Inspect(cardObject.Span, evaluationTime);
        if (!inspection.ProfileDiagnostics.IsEmpty)
            return Result.Failure<BiometricVerificationReport>(
                "The card object needs profile review."
            );

        // The application supplies expected identity from its authenticated card
        // context. The provider returns integrity, envelope, trust, and revocation
        // findings; the application combines them into its authentication decision.
        var report = await BiometricSignatureWorkflow.VerifyAsync(
            parsed.Value.Record,
            expectedIdentity,
            verifier,
            cancellationToken
        );
        return report.MapError(error => error.Message);
    }
}
