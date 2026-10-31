namespace OpenPhysical.Domains.Cbeff;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Biometrics;
using CSharpFunctionalExtensions;

/// <summary>
/// Represents biometric data to be encoded in CBEFF format.
/// This is an immutable value object that encapsulates all the information
/// needed to create a CBEFF payload.
/// </summary>
public sealed record BiometricData
{
    /// <summary>
    /// Gets the biometric modality (face, fingerprint, iris, etc.).
    /// </summary>
    public BiometricModality Modality { get; }

    /// <summary>
    /// Gets the raw biometric data bytes.
    /// For facial images, this would be JPEG 2000 or similar image data.
    /// For fingerprints, this would be minutiae template data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the biometric data format specification.
    /// </summary>
    public BiometricFormat Format { get; }

    /// <summary>
    /// Gets the date when the biometric data was captured.
    /// </summary>
    public DateTime CaptureDate { get; }

    /// <summary>
    /// Gets the biometric quality score (-2 to 100, -1 = not supported).
    /// Special PIV values: -2 (254 as byte) = permanently unusable, -1 (255 as byte) = temporarily unusable.
    /// </summary>
    public sbyte Quality { get; }

    /// <summary>
    /// Gets the Federal Agency Smart Credential Number (25 bytes).
    /// Required for PIV cards, optional for other uses.
    /// </summary>
    public byte[]? FascN { get; }

    /// <summary>
    /// Constructor for BiometricData.
    /// </summary>
    public BiometricData(
        BiometricModality modality,
        byte[] data,
        BiometricFormat format,
        DateTime captureDate,
        sbyte quality = -1,
        byte[]? fascN = null
    )
    {
        Modality = modality;
        Data = data;
        Format = format;
        CaptureDate = captureDate;
        Quality = quality;
        FascN = fascN;
    }

    /// <summary>
    /// Creates a new BiometricData instance with validation.
    /// </summary>
    public static Result<BiometricData> Create(
        BiometricModality modality,
        byte[] data,
        BiometricFormat format,
        DateTime captureDate,
        sbyte quality = -1,
        byte[]? fascN = null
    )
    {
        if (modality == null)
            return Result.Failure<BiometricData>("Modality cannot be null");

        if (data == null || data.Length == 0)
            return Result.Failure<BiometricData>("Biometric data cannot be null or empty");

        if (format == null)
            return Result.Failure<BiometricData>("Format cannot be null");

        if (captureDate > DateTime.UtcNow.AddDays(1))
            return Result.Failure<BiometricData>("Capture date cannot be in the future");

        // Valid quality ranges: -2 to 100
        if (quality < -2 || quality > 100)
            return Result.Failure<BiometricData>($"Invalid quality value: {quality}");

        if (fascN != null && fascN.Length != 25)
            return Result.Failure<BiometricData>("FASC-N must be exactly 25 bytes when provided");

        return Result.Success(
            new BiometricData(
                modality: modality,
                data: (byte[])data.Clone(),
                format: format,
                captureDate: captureDate,
                quality: quality,
                fascN: fascN != null ? (byte[])fascN.Clone() : null
            )
        );
    }

    /// <summary>
    /// Creates BiometricData for a facial image.
    /// </summary>
    public static Result<BiometricData> CreateFacialImage(
        byte[] imageData,
        DateTime captureDate,
        sbyte quality = -1,
        byte[]? fascN = null
    ) =>
        Create(
            BiometricModality.Face,
            imageData,
            BiometricFormat.WellKnown.Incits385FaceImage,
            captureDate,
            quality,
            fascN
        );

    /// <summary>
    /// Creates BiometricData for fingerprint minutiae.
    /// </summary>
    public static Result<BiometricData> CreateFingerprintMinutiae(
        byte[] minutiaeData,
        DateTime captureDate,
        sbyte quality = -1,
        byte[]? fascN = null
    ) =>
        Create(
            BiometricModality.Fingerprint,
            minutiaeData,
            BiometricFormat.WellKnown.Incits378Minutiae,
            captureDate,
            quality,
            fascN
        );

    /// <summary>
    /// Gets a copy of the biometric data to maintain immutability.
    /// </summary>
    public byte[] GetData() => (byte[])Data.Clone();

    /// <summary>
    /// Gets a copy of the FASC-N to maintain immutability.
    /// </summary>
    public byte[]? GetFascN() => FascN != null ? (byte[])FascN.Clone() : null;

    /// <summary>
    /// Validates this instance for PIV compliance.
    /// </summary>
    public Result ValidateForPiv()
    {
        if (FascN == null || FascN.Length != 25)
            return Result.Failure("PIV requires a 25-byte FASC-N");

        if (Modality != BiometricModality.Face && Modality != BiometricModality.Fingerprint)
            return Result.Failure("PIV only supports face and fingerprint modalities");

        if (
            Format != BiometricFormat.WellKnown.Incits385FaceImage
            && Format != BiometricFormat.WellKnown.Incits378Minutiae
            && Format != BiometricFormat.WellKnown.PivPatronFormatA
        )
            return Result.Failure("Invalid format for PIV");

        return Result.Success();
    }
}
