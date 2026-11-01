// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using OpenPhysical.Biometrics.NFIQ1;

internal sealed class PoorImageEngine(Nfiq1AssessmentStatus status) : INfiq1Engine
{
    public Nfiq1Assessment Assess(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int dotsPerInch
    ) => new(5, 1, status);
}
