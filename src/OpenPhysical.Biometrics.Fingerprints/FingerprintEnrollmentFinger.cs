// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using OpenPhysical.Biometrics.NBIS;

/// <summary>A selected enrollment finger, including usable captures and unavailable states.</summary>
public sealed class FingerprintEnrollmentFinger
{
    private FingerprintEnrollmentFinger(
        FingerPosition finger,
        FingerprintAvailability availability,
        FingerprintCapture? capture,
        FingerprintTemplate? template,
        FingerprintQualityAssessment? quality
    )
    {
        Finger = finger;
        Availability = availability;
        Capture = capture;
        Template = template;
        Quality = quality;
    }

    /// <summary>Gets the selected finger.</summary>
    public FingerPosition Finger { get; }

    /// <summary>Gets the availability recorded for issuance.</summary>
    public FingerprintAvailability Availability { get; }

    /// <summary>Gets the capture when this finger is available.</summary>
    public FingerprintCapture? Capture { get; }

    /// <summary>Gets the extracted matching template when this finger is available.</summary>
    public FingerprintTemplate? Template { get; }

    /// <summary>Gets the parent image's NFIQ 1 assessment when available.</summary>
    public FingerprintQualityAssessment? Quality { get; }

    /// <summary>Gets whether quality or a low minutia count recommends another acquisition.</summary>
    public bool ReacquisitionRecommended =>
        Availability == FingerprintAvailability.Available
        && (
            Quality!.Status != FingerprintQualityStatus.Assessed
            || Quality.Nfiq1 > 3
            || Template!.Minutiae.Length <= 10
        );

    /// <summary>Records a finger that is temporarily or permanently unusable.</summary>
    /// <param name="finger">The unavailable finger.</param>
    /// <param name="availability">The temporary or permanent unavailable state.</param>
    /// <returns>An enrollment selection containing no captured biometric data.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A finger or unavailable state is invalid.</exception>
    public static FingerprintEnrollmentFinger Unavailable(
        FingerPosition finger,
        FingerprintAvailability availability
    )
    {
        if (!Enum.IsDefined(finger))
            throw new ArgumentOutOfRangeException(nameof(finger));
        if (
            availability
            is not (
                FingerprintAvailability.TemporarilyUnusable
                or FingerprintAvailability.PermanentlyUnusable
            )
        )
            throw new ArgumentOutOfRangeException(nameof(availability));
        return new(finger, availability, null, null, null);
    }

    internal static FingerprintEnrollmentFinger Available(
        FingerprintCapture capture,
        FingerprintTemplate template,
        FingerprintQualityAssessment quality
    ) => new(capture.Finger, FingerprintAvailability.Available, capture, template, quality);
}

/// <summary>Identifies the extractor and the record geometry used during PIV issuance.</summary>
public sealed class PivFingerprintRecordMetadata
{
    /// <summary>Creates the registered identifiers and explicit original-image geometry.</summary>
    /// <param name="productOwner">Registered extractor vendor identifier, greater than zero.</param>
    /// <param name="productType">Registered extractor product/version identifier, greater than zero.</param>
    /// <param name="equipmentId">Registered capture device identifier, from 1 through 4095.</param>
    /// <param name="width">Largest original image width in pixels.</param>
    /// <param name="height">Largest original image height in pixels.</param>
    /// <param name="equipmentAppendixFCertified">Whether the acquisition equipment carries Appendix F certification.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identifier or dimension is invalid.</exception>
    public PivFingerprintRecordMetadata(
        ushort productOwner,
        ushort productType,
        ushort equipmentId,
        ushort width,
        ushort height,
        bool equipmentAppendixFCertified
    )
    {
        if (productOwner == 0)
            throw new ArgumentOutOfRangeException(nameof(productOwner));
        if (productType == 0)
            throw new ArgumentOutOfRangeException(nameof(productType));
        if (equipmentId is 0 or > 4095)
            throw new ArgumentOutOfRangeException(nameof(equipmentId));
        if (width is < 1 or > 2048)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 1 or > 2048)
            throw new ArgumentOutOfRangeException(nameof(height));
        ProductOwner = productOwner;
        ProductType = productType;
        EquipmentId = equipmentId;
        Width = width;
        Height = height;
        EquipmentAppendixFCertified = equipmentAppendixFCertified;
    }

    /// <summary>Gets the registered extractor vendor identifier.</summary>
    public ushort ProductOwner { get; }

    /// <summary>Gets the registered extractor product/version identifier.</summary>
    public ushort ProductType { get; }

    /// <summary>Gets the registered capture device identifier.</summary>
    public ushort EquipmentId { get; }

    /// <summary>Gets the largest original image width in pixels.</summary>
    public ushort Width { get; }

    /// <summary>Gets the largest original image height in pixels.</summary>
    public ushort Height { get; }

    /// <summary>Gets the acquisition equipment certification supplied by the application.</summary>
    public bool EquipmentAppendixFCertified { get; }
}
