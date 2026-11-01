// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Facial;

/// <summary>Describes the expected outcome of preparing a facial record.</summary>
public enum FacialPreparationStatus
{
    /// <summary>The facial record was prepared and its encoded metadata passed the profile.</summary>
    Prepared,

    /// <summary>Acquisition evidence needs operator review.</summary>
    AcquisitionReviewRequired,

    /// <summary>The supplied geometry or metadata fails the selected profile.</summary>
    ProfileRejected,

    /// <summary>The encoded image exceeded the permitted compression ratio.</summary>
    CompressionRejected,
}
