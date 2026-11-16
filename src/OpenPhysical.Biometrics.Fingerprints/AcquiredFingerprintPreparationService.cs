// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using Org.BouncyCastle.Crypto.Digests;

/// <summary>Reuses acquisition validation and enrollment processing to produce unsigned record lineage.</summary>
public sealed class AcquiredFingerprintPreparationService
{
    private readonly AcquiredFingerprintAdapter adapter;
    private readonly PivFingerprintEnrollmentService enrollment;

    /// <summary>Creates the service from shared acquisition and enrollment operations.</summary>
    /// <param name="adapter">Approved capture mapping.</param>
    /// <param name="enrollment">Existing quality, extraction and record operations.</param>
    public AcquiredFingerprintPreparationService(
        AcquiredFingerprintAdapter adapter,
        PivFingerprintEnrollmentService enrollment
    )
    {
        this.adapter = adapter;
        this.enrollment = enrollment;
    }

    /// <summary>Assesses and extracts the exact qualified capture once.</summary>
    /// <param name="frame">Corrected qualified frame.</param>
    /// <param name="finger">Selected finger.</param>
    /// <param name="profile">Authority-approved acquisition mapping.</param>
    /// <returns>A reusable selection with its original acquisition lineage, or a typed failure.</returns>
    public Result<PreparedAcquiredFingerprint, FingerprintError> Prepare(
        CaptureFrame frame,
        FingerPosition finger,
        FingerprintAcquisitionProfile profile
    ) =>
        adapter
            .Adapt(frame, finger, profile)
            .Bind(enrollment.Prepare)
            .Map(selection => new PreparedAcquiredFingerprint(selection, frame, profile));

    /// <summary>Encodes the selected views and binds their lineage to the complete unsigned record.</summary>
    /// <param name="primary">Prepared primary selection.</param>
    /// <param name="secondary">Prepared secondary selection.</param>
    /// <param name="metadata">Explicit registered product and equipment metadata.</param>
    /// <returns>Canonical bytes and ordered evidence, or a typed validation/encoding failure.</returns>
    public Result<PreparedFingerprintRecord, FingerprintError> CreateRecord(
        PreparedAcquiredFingerprint primary,
        PreparedAcquiredFingerprint secondary,
        PivFingerprintRecordMetadata metadata
    )
    {
        if (primary is null || secondary is null || metadata is null)
            return new FingerprintError(
                FingerprintErrorCodes.InvalidPreparation,
                "Two prepared selections and explicit record metadata are required."
            );
        return enrollment
            .CreateRecord(primary.Selection, secondary.Selection, metadata)
            .Bind(record =>
                Incits378Codec
                    .Write(record)
                    .MapError(_ => new FingerprintError(
                        FingerprintErrorCodes.InvalidPreparation,
                        "The prepared views could not be encoded as an INCITS record."
                    ))
            )
            .Map(bytes => BindEvidence(bytes, primary, secondary));
    }

    private static PreparedFingerprintRecord BindEvidence(
        byte[] bytes,
        PreparedAcquiredFingerprint primary,
        PreparedAcquiredFingerprint secondary
    )
    {
        var digest = Hash(bytes);
        return new PreparedFingerprintRecord(
            ImmutableArray.Create(bytes),
            [Evidence(primary, digest), Evidence(secondary, digest)]
        );
    }

    private static FingerprintPreparationEvidence Evidence(
        PreparedAcquiredFingerprint selection,
        string recordSha256
    ) => new(selection, recordSha256, selection.Frame.Map(frame => Hash(frame.Pixels.AsSpan())));

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        var digest = new Sha256Digest();
        digest.BlockUpdate(bytes);
        var output = new byte[32];
        _ = digest.DoFinal(output);
        return Convert.ToHexStringLower(output);
    }
}
