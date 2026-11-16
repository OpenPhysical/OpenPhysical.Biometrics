// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;

internal static class EnrollmentTransferValidation
{
    private const long MaximumArtifact = 67_108_864;
    private const long MaximumAggregate = 134_217_728;
    private const int MaximumManifest = 1_048_576;

    internal static bool Validate(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        ReadOnlySpan<byte> recipient,
        out PackageError error
    )
    {
        error = new PackageError(
            PackageErrorCodes.InvalidTransfer,
            "Transfer metadata is invalid."
        );
        if (!ValidMetadata(transfer, sender, recipient) || !WithinLimits(transfer))
            return false;
        if (!OriginMatchesArtifacts(transfer))
            return false;
        error = null!;
        return true;
    }

    private static bool ValidMetadata(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        ReadOnlySpan<byte> recipient
    ) =>
        ValidIdentity(transfer)
        && ValidTimes(transfer)
        && ValidKeys(sender, recipient)
        && ReferencesValid(transfer.AuthorizationReferences);

    private static bool ValidIdentity(EnrollmentEnvelopeTransfer transfer) =>
        !transfer.Artifacts.IsDefault
        && transfer.Artifacts.Length <= 63
        && Enum.IsDefined(transfer.Purpose)
        && OpaqueValid(transfer.OrganizationId)
        && OpaqueValid(transfer.TransferId)
        && OpaqueValid(transfer.SenderId)
        && OpaqueValid(transfer.RecipientId)
        && OpaqueValid(transfer.EnrollmentId);

    private static bool ValidTimes(EnrollmentEnvelopeTransfer transfer) =>
        transfer.CreatedUtc.Offset == TimeSpan.Zero
        && transfer.CreatedUtc.ToUnixTimeSeconds() > 0
        && transfer.ExpiresUtc > transfer.CreatedUtc
        && transfer.ExpiresUtc - transfer.CreatedUtc <= TimeSpan.FromDays(365)
        && transfer.CreatedUtc.Ticks % TimeSpan.TicksPerSecond == 0
        && transfer.ExpiresUtc.Ticks % TimeSpan.TicksPerSecond == 0;

    private static bool ValidKeys(IEnrollmentSigner sender, ReadOnlySpan<byte> recipient) =>
        sender.KeyId.Length == 32
        && sender.SubjectPublicKeyInfo.Length > 0
        && EnrollmentOriginArtifactCodec.KeyIdMatches(
            sender.SubjectPublicKeyInfo.Span,
            sender.KeyId.Span
        )
        && recipient.Length > 0;

    private static bool ReferencesValid(ImmutableArray<string> references) =>
        !references.IsDefault
        && references.Length <= 64
        && references.All(ReferenceValid)
        && references.Distinct(StringComparer.Ordinal).Count() == references.Length;

    private static bool WithinLimits(EnrollmentEnvelopeTransfer transfer)
    {
        long total = EnrollmentOriginArtifactCodec.WriteSigned(transfer.Origin).Length;
        if (total > MaximumManifest)
            return false;
        foreach (var artifact in transfer.Artifacts)
        {
            if (artifact.Bytes.Length > MaximumArtifact)
                return false;
            total += artifact.Bytes.Length;
        }
        return total <= MaximumAggregate;
    }

    private static bool OriginMatchesArtifacts(EnrollmentEnvelopeTransfer transfer) =>
        transfer.Origin.OrganizationId == transfer.OrganizationId
        && transfer.Origin.EnrollmentId == transfer.EnrollmentId
        && transfer.Origin.Revision == transfer.Revision
        && EnrollmentOriginArtifactCodec.ValidateArtifacts(
            transfer.Origin,
            transfer.Artifacts.Select(a => a.Bytes.ToArray()).ToArray()
        );

    private static bool OpaqueValid(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    private static bool ReferenceValid(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 512
        && value.All(c => c is >= (char)0x21 and <= (char)0x7e);
}
