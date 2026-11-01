// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Facial;

/// <summary>Records the camera and operator findings attached to the captured sRGB pixels.</summary>
/// <param name="CapturedAt">The acquisition instant.</param>
/// <param name="DeviceType">The camera's device identifier.</param>
/// <param name="SourceType">Two for a digital still camera or six for digital video.</param>
/// <param name="FullFrontalConfirmed">Whether acquisition confirmed a single neutral, full-frontal subject.</param>
/// <param name="OpticalResolutionConfirmed">Whether acquisition confirmed the required optical resolution and geometry.</param>
/// <param name="Quality">Recorded facial quality from zero through 100; zero denotes unspecified quality.</param>
public sealed record FacialAcquisitionMetadata(
    DateTimeOffset CapturedAt,
    ushort DeviceType,
    byte SourceType,
    bool FullFrontalConfirmed,
    bool OpticalResolutionConfirmed,
    ushort Quality = 0
);
