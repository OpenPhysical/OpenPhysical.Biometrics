// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

internal static class TestCaptures
{
    internal static FingerprintCapture Capture(FingerPosition finger) =>
        Capture(finger, new byte[128 * 128]);

    internal static FingerprintCapture Capture(FingerPosition finger, byte[] pixels) =>
        FingerprintCapture
            .Create(
                pixels,
                128,
                128,
                500,
                finger,
                DateTimeOffset.UnixEpoch,
                FingerprintImpression.PlainLiveScan,
                3,
                equipmentAppendixFCertified: true
            )
            .Value;

    internal static PivFingerprintRecordMetadata Metadata() =>
        PivFingerprintRecordMetadata
            .Create(1, 2, 3, 128, 128, equipmentAppendixFCertified: true)
            .Value;
}
