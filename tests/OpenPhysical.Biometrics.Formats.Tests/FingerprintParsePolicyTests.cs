// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using System.Buffers.Binary;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Checks explicit coordinate interpretation without changing original point indices.</summary>
[TestFixture]
public sealed class FingerprintParsePolicyTests
{
    /// <summary>Retains original minutiae and ridge references while strict parsing and writing reject them.</summary>
    [Test]
    public void ExplicitPolicyPreservesWireIndicesAndLocatesTheWarning()
    {
        var original = CodecTestData.Fingerprint();
        var view = original.Views[0] with
        {
            Minutiae = [new(1, 10, 20, 45, 100), new(2, 30, 40, 60, 80)],
            ExtendedBlocks = [new(1, [0, 1, 2, 7])],
        };
        var bytes = Incits378Codec.Write(original with { Views = [view] }).Value;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(30), (ushort)(0x4000 | original.Width));
        Incits378Codec.Parse(bytes).Should().Fail().Which.Code.Should().Be("Minutia");
        var parsed = Incits378Codec.Parse(
            bytes,
            new(),
            FingerprintParsePolicy.PreserveOutOfBoundsMinutiae
        );
        parsed.Should().Succeed();
        parsed.Value.Record.Views[0].Minutiae.Should().HaveCount(2);
        parsed.Value.Record.Views[0].Minutiae[0].X.Should().Be(original.Width);
        parsed.Value.Record.Views[0].Minutiae[1].Should().Be(view.Minutiae[1]);
        parsed
            .Value.Record.Views[0]
            .ExtendedBlocks[0]
            .Data.Should()
            .Equal(view.ExtendedBlocks[0].Data);
        var warning = parsed.Value.Inspection.Diagnostics.Should().ContainSingle().Which;
        (warning.Code, warning.Offset, warning.Length, warning.Severity)
            .Should()
            .Be(("MinutiaOutsideImage", 30, 4, FormatDiagnosticSeverity.Warning));
        parsed.Value.Inspection.Fields.All(field => field.Value.HasNoValue).Should().BeTrue();
        Incits378Codec.Write(parsed.Value.Record).Should().Fail();
    }

    /// <summary>Geometry interpretation never relaxes reserved bits, angle, quality or type checks.</summary>
    /// <param name="offset">Field byte to corrupt.</param>
    /// <param name="value">Invalid field value.</param>
    [TestCase(30, 0xc0)]
    [TestCase(32, 0xc0)]
    [TestCase(34, 180)]
    [TestCase(35, 101)]
    public void AlternateGeometryPolicyStillRejectsMalformedFields(int offset, byte value)
    {
        var bytes = CodecTestData.FingerprintBytes();
        bytes[offset] = value;
        Incits378Codec
            .Parse(bytes, new(), FingerprintParsePolicy.PreserveOutOfBoundsMinutiae)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Minutia");
    }

    /// <summary>Unknown enum values cannot select a permissive interpretation.</summary>
    [Test]
    public void UnknownPolicyFailsExplicitly()
    {
        Incits378Codec
            .Parse(CodecTestData.FingerprintBytes(), new(), (FingerprintParsePolicy)999)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("ParsePolicy");
    }
}
