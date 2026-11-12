// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Identifies a one-to-one geometric correspondence without changing either template.</summary>
/// <param name="ReferenceIndex">Index in the original reference minutiae array.</param>
/// <param name="ProbeIndex">Index in the original probe minutiae array.</param>
public sealed record FingerprintAlignmentPair(int ReferenceIndex, int ProbeIndex);
