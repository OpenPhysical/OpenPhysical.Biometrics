// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Checks that truncated input fails with diagnostics instead of throwing.</summary>
[TestFixture]
public sealed class TruncationTests
{
    /// <summary>Returns format diagnostics for every truncated prefix.</summary>
    [Test]
    public void EveryTruncatedPrefixReturnsDiagnostics()
    {
        var fingerprint = CodecTestData.FingerprintBytes();
        var face = Incits385Codec.Write(CodecTestData.Face()).Value;
        var cbeff = CbeffCodec.Write(CodecTestData.Cbeff()).Value;
        for (var i = 0; i < fingerprint.Length; i++)
            Incits378Codec.Parse(fingerprint.AsSpan(0, i)).Should().Fail();
        for (var i = 0; i < face.Length; i++)
            Incits385Codec.Parse(face.AsSpan(0, i)).Should().Fail();
        for (var i = 0; i < cbeff.Length; i++)
            CbeffCodec.Parse(cbeff.AsSpan(0, i)).Should().Fail();
    }
}
