// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;

/// <summary>An immutable grayscale capture with explicit acquisition metadata.</summary>
public sealed class FingerprintCapture
{
    private FingerprintCapture(
        FingerprintImage image,
        FingerPosition finger,
        DateTimeOffset capturedAt,
        FingerprintImpression impression,
        ushort equipmentId,
        bool equipmentAppendixFCertified
    )
    {
        Image = image;
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
    /// <returns>The capture, or an error when dimensions, resolution, pixel length, or metadata are invalid.</returns>
    public static Result<FingerprintCapture, FingerprintError> Create(
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
        var metadata = CheckMetadata(finger, impression, equipmentId);
        if (metadata.IsFailure)
            return Result.Failure<FingerprintCapture, FingerprintError>(metadata.Error);
        var owned = pixels.ToArray();
        var image = FingerprintEdge.Try(
            () => new FingerprintImage(owned, width, height, dotsPerInch),
            FingerprintErrorCodes.InvalidCapture,
            "The capture dimensions, resolution, or pixel length are invalid."
        );
        return image.Map(value => new FingerprintCapture(
            value,
            finger,
            capturedAt,
            impression,
            equipmentId,
            equipmentAppendixFCertified
        ));
    }

    private static UnitResult<FingerprintError> CheckMetadata(
        FingerPosition finger,
        FingerprintImpression impression,
        ushort equipmentId
    ) =>
        FingerprintChecks
            .Require(
                Enum.IsDefined(finger),
                FingerprintErrorCodes.InvalidFinger,
                "The finger position is not an ANSI finger code."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    Enum.IsDefined(impression),
                    FingerprintErrorCodes.InvalidImpression,
                    "The impression type is not supported."
                )
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    equipmentId is >= 1 and <= 4095,
                    FingerprintErrorCodes.InvalidEquipment,
                    "The equipment identifier must be from 1 through 4095."
                )
            );
}
