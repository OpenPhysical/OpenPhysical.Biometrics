// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using OpenPhysical.Biometrics.NBIS;

/// <summary>An immutable grayscale capture with explicit acquisition metadata.</summary>
public sealed class FingerprintCapture
{
    /// <summary>Copies pixels and binds them to the finger and acquisition metadata.</summary>
    /// <param name="pixels">Top-left-origin, tightly packed grayscale pixels; zero is black.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="dotsPerInch">Explicit nominal resolution, currently 500 pixels per inch.</param>
    /// <param name="finger">The captured finger.</param>
    /// <param name="capturedAt">The acquisition instant, including its UTC offset.</param>
    /// <param name="impression">The acquisition method.</param>
    /// <param name="equipmentId">The registered capture-equipment identifier, from 1 through 4095.</param>
    /// <param name="equipmentAppendixFCertified">Whether the acquisition equipment carries Appendix F certification.</param>
    /// <exception cref="ArgumentOutOfRangeException">Dimensions, resolution, or metadata are outside their accepted ranges.</exception>
    /// <exception cref="ArgumentException">Pixel length differs from the dimensions.</exception>
    public FingerprintCapture(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int dotsPerInch,
        FingerPosition finger,
        DateTimeOffset capturedAt,
        FingerprintImpression impression,
        ushort equipmentId,
        bool equipmentAppendixFCertified
    )
    {
        if (!Enum.IsDefined(finger))
            throw new ArgumentOutOfRangeException(nameof(finger));
        if (!Enum.IsDefined(impression))
            throw new ArgumentOutOfRangeException(nameof(impression));
        if (equipmentId is 0 or > 4095)
            throw new ArgumentOutOfRangeException(nameof(equipmentId));
        Image = new FingerprintImage(pixels, width, height, dotsPerInch);
        Finger = finger;
        CapturedAt = capturedAt;
        Impression = impression;
        EquipmentId = equipmentId;
        EquipmentAppendixFCertified = equipmentAppendixFCertified;
    }

    /// <summary>Gets the capture-owned immutable pixels and geometry.</summary>
    public FingerprintImage Image { get; }

    /// <summary>Gets the finger represented by this image.</summary>
    public FingerPosition Finger { get; }

    /// <summary>Gets the recorded acquisition instant.</summary>
    public DateTimeOffset CapturedAt { get; }

    /// <summary>Gets the acquisition method.</summary>
    public FingerprintImpression Impression { get; }

    /// <summary>Gets the registered capture-equipment identifier.</summary>
    public ushort EquipmentId { get; }

    /// <summary>Gets the equipment certification recorded at acquisition.</summary>
    public bool EquipmentAppendixFCertified { get; }
}
