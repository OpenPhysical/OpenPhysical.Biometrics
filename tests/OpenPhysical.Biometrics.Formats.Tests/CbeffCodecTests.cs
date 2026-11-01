// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using System.Buffers.Binary;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Exercises exact CBEFF header preservation and structural boundaries.</summary>
[TestFixture]
public sealed class CbeffCodecTests
{
    /// <summary>Checks exact CBEFF header preservation and detached signed-content boundaries.</summary>
    [Test]
    public void CbeffPreservesExactHeaderAndSignedContent()
    {
        var record = CodecTestData.Cbeff();
        var bytes = CbeffCodec.Write(record).Value;
        var parsed = CbeffCodec.Parse(bytes);
        parsed.Should().Succeed();
        using (new AssertionScope())
        {
            bytes.Length.Should().Be(93);
            parsed.Value.Record.Header.RawBytes.Should().Equal(record.Header.RawBytes);
            CbeffCodec.GetSignedData(parsed.Value.Record).Value.Should().Equal(bytes[..91]);
            CbeffCodec.Write(parsed.Value.Record).Value.Should().Equal(bytes);
            bytes[12].Should().Be(20);
            bytes[13].Should().Be(26);
        }
        var headerCopy = record.Header.ToArray();
        headerCopy[0] = 0;
        record.Header.PatronHeaderVersion.Should().Be(3);
        record.Header.WithSignatureLength(42).SignatureLength.Should().Be(42);
        record.Header.SignatureLength.Should().Be(2);
    }

    /// <summary>Rejects malformed calendar timestamps and length mismatches.</summary>
    [Test]
    public void CbeffCalendarAndLengthErrorsAreLocated()
    {
        var bytes = CbeffCodec.Write(CodecTestData.Cbeff()).Value;
        bytes[14] = 13;
        CbeffCodec.Parse(bytes).Should().Fail().Which.Code.Should().Be("CbeffTimestamp");
        bytes = CbeffCodec.Write(CodecTestData.Cbeff()).Value;
        bytes[7]++;
        CbeffCodec.Parse(bytes).Should().Fail().Which.Diagnostics[0].Offset.Should().Be(2);
    }

    /// <summary>Rejects oversized unsigned lengths before any buffer slice.</summary>
    /// <param name="declaredLength">A biometric block length exceeding the supplied buffer.</param>
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public void CbeffRejectsUnsignedLengthsBeyondTheInput(uint declaredLength)
    {
        var bytes = CbeffCodec.Write(CodecTestData.Cbeff()).Value;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(2, 4), declaredLength);
        var error = CbeffCodec.Parse(bytes).Should().Fail().Which;
        using (new AssertionScope())
        {
            error.Diagnostics[0].Code.Should().Be("CbeffLength");
            error.Diagnostics[0].Offset.Should().Be(2);
        }
    }

    /// <summary>Reports inconsistent block lengths as failed results.</summary>
    [Test]
    public void InconsistentRecordsFailWithStableCodes()
    {
        var record = CodecTestData.Cbeff();
        using (new AssertionScope())
        {
            CbeffCodec
                .Write(record with { Signature = [1] })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("CbeffLength");
            CbeffCodec
                .GetSignedData(record with { BiometricData = [1] })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("CbeffLength");
        }
    }

    /// <summary>Reports invalid header fields as failed results.</summary>
    [Test]
    public void InvalidHeaderFieldsFailWithStableCodes()
    {
        var now = DateTimeOffset.UnixEpoch;
        using (new AssertionScope())
        {
            CbeffHeader
                .Create(0, 0, 0, 0, now, now, now, 0, 0, 0, new byte[17], new byte[25])
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("CbeffCreator");
            CbeffHeader
                .Create(0, 0, 0, 0, now, now, now, 0, 0, 0, new byte[18], new byte[24])
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("CbeffFascN");
            CbeffHeader
                .Create(0, 0, 0, 0, now, now, now, 0x1000000, 0, 0, new byte[18], new byte[25])
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("CbeffBiometricType");
            CbeffHeader
                .FromBytes(new byte[87])
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("CbeffHeaderLength");
        }
    }
}
