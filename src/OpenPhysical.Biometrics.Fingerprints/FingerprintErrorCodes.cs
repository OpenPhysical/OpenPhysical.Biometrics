// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Defines the stable error codes returned by fingerprint operations.</summary>
public static class FingerprintErrorCodes
{
    /// <summary>The finger position is not an ANSI finger code.</summary>
    public const string InvalidFinger = "InvalidFinger";

    /// <summary>The view number exceeds fifteen.</summary>
    public const string InvalidViewNumber = "InvalidViewNumber";

    /// <summary>The impression type is not a supported acquisition method.</summary>
    public const string InvalidImpression = "InvalidImpression";

    /// <summary>The availability state is not valid for the operation.</summary>
    public const string InvalidAvailability = "InvalidAvailability";

    /// <summary>The capture-equipment identifier is outside its registered range.</summary>
    public const string InvalidEquipment = "InvalidEquipment";

    /// <summary>The capture image rejected its dimensions, resolution, or pixel length.</summary>
    public const string InvalidCapture = "InvalidCapture";

    /// <summary>The record resolution is not the nominal 197 pixels per centimetre.</summary>
    public const string UnsupportedResolution = "UnsupportedResolution";

    /// <summary>The record contains uninitialized views.</summary>
    public const string UninitializedViews = "UninitializedViews";

    /// <summary>The selection does not identify exactly one existing finger view.</summary>
    public const string ViewSelection = "ViewSelection";

    /// <summary>The selected finger is marked unusable.</summary>
    public const string FingerUnavailable = "FingerUnavailable";

    /// <summary>The selected view has an invalid quality or minutia count.</summary>
    public const string InvalidView = "InvalidView";

    /// <summary>More than 150 minutiae were selected without reported quality.</summary>
    public const string UnreportedQualityLimit = "UnreportedQualityLimit";

    /// <summary>A minutia has an invalid type, coordinate, angle, or quality.</summary>
    public const string InvalidMinutia = "InvalidMinutia";

    /// <summary>The matching template rejected its dimensions or minutiae.</summary>
    public const string InvalidTemplate = "InvalidTemplate";

    /// <summary>The probe and selected reference fingers differ.</summary>
    public const string FingerMismatch = "FingerMismatch";

    /// <summary>The matching policy identifier is blank.</summary>
    public const string InvalidPolicy = "InvalidPolicy";

    /// <summary>A comparison score or minimum score is negative.</summary>
    public const string InvalidScore = "InvalidScore";

    /// <summary>The quality classification, confidence, status, or algorithm identity is invalid.</summary>
    public const string InvalidQuality = "InvalidQuality";

    /// <summary>The quality engine returned an unrecognized status.</summary>
    public const string UnknownQualityStatus = "UnknownQualityStatus";

    /// <summary>The quality engine failed.</summary>
    public const string QualityEngineFailed = "QualityEngineFailed";

    /// <summary>The extraction or matching engine failed.</summary>
    public const string EngineFailed = "EngineFailed";

    /// <summary>A ranking input contains a finger without a capture.</summary>
    public const string UnavailableCandidate = "UnavailableCandidate";

    /// <summary>PIV enrollment requires Appendix F certified equipment.</summary>
    public const string EquipmentCertification = "EquipmentCertification";

    /// <summary>The primary and secondary selections name the same finger.</summary>
    public const string DuplicateFingers = "DuplicateFingers";

    /// <summary>An unavailable primary finger precedes an available secondary finger.</summary>
    public const string PrimaryMustBeAvailable = "PrimaryMustBeAvailable";

    /// <summary>The record metadata dimensions differ from the largest source dimensions.</summary>
    public const string RecordDimensions = "RecordDimensions";

    /// <summary>A capture's equipment differs from the record metadata.</summary>
    public const string EquipmentMismatch = "EquipmentMismatch";

    /// <summary>A registered product identifier is zero.</summary>
    public const string InvalidProduct = "InvalidProduct";
}
