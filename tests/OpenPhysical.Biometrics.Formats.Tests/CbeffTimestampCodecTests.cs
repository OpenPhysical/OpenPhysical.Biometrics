// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Checks the eight-byte CBEFF timestamp encoding.</summary>
[TestFixture]
public sealed class CbeffTimestampCodecTests
{
    /// <summary>Encodes in UTC and returns buffers the caller owns.</summary>
    [Test]
    public void IntegerPairsUseUtcAndCallerOwnsEncodedBuffers()
    {
        var instant = new DateTimeOffset(2005, 12, 15, 18, 35, 30, TimeSpan.FromHours(1));
        var encoded = CbeffTimestampCodec.Encode(instant);
        encoded.Should().Equal(new byte[] { 20, 5, 12, 15, 17, 35, 30, 90 });
        var decoded = CbeffTimestampCodec.Decode(encoded);
        encoded[2] = 1;
        decoded.Value.Should().Be(instant.ToUniversalTime());
        CbeffTimestampCodec.Encode(instant)[2].Should().Be(12);
    }

    /// <summary>Fails malformed timestamps with a stable code instead of an exception.</summary>
    /// <param name="value">The malformed encoded timestamp.</param>
    [TestCase(new byte[] { 20, 5, 2, 30, 17, 35, 30, 90 })]
    [TestCase(new byte[] { 20, 5, 12, 15, 17, 35, 30, 0 })]
    [TestCase(new byte[] { 20, 100, 12, 15, 17, 35, 30, 90 })]
    [TestCase(new byte[] { 20, 5, 12 })]
    [TestCase(new byte[] { 0, 0, 1, 1, 0, 0, 0, 90 })]
    public void MalformedTimestampsFailExplicitly(byte[] value) =>
        CbeffTimestampCodec
            .Decode(value)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CbeffTimestampCodec.InvalidTimestampCode);
}
