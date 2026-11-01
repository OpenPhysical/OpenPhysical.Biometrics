// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using OpenPhysical.Biometrics.NBIS;
using MatchingMinutia = OpenPhysical.Biometrics.NBIS.FingerprintMinutia;

internal sealed class TestEngine : IFingerprintEngine
{
    internal int Extractions { get; private set; }

    public FingerprintTemplate Extract(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int dotsPerInch
    )
    {
        Extractions++;
        return new(
            width,
            height,
            Enumerable
                .Range(0, 200)
                .Select(index => new MatchingMinutia(
                    index % 128,
                    1 + index % 128,
                    0,
                    index % 101,
                    1
                )),
            "test-engine"
        );
    }

    public int Match(FingerprintTemplate probe, FingerprintTemplate gallery) => 42;
}
