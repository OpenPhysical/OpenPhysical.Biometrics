// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Exercises INCITS 378 serialization limits and view states.</summary>
[TestFixture]
public sealed class FingerprintWriteTests
{
    /// <summary>Preserves explicit unavailable-finger statuses with empty minutiae collections.</summary>
    /// <param name="quality">The PIV status identifying a temporary or permanent capture unavailability.</param>
    [TestCase(254)]
    [TestCase(255)]
    public void UnavailableViewsRemainEmpty(byte quality)
    {
        var original = CodecTestData.Fingerprint();
        var bytes = Incits378Codec
            .Write(
                original with
                {
                    Views = [original.Views[0] with { Quality = quality, Minutiae = [] }],
                }
            )
            .Value;
        var parsed = Incits378Codec.Parse(bytes).Value.Record;
        parsed.Views[0].Quality.Should().Be(quality);
        parsed.Views[0].Minutiae.Should().BeEmpty();
    }

    /// <summary>Reports unencodable fields as failed results with stable codes.</summary>
    [Test]
    public void WritingUnencodableFieldsFailsWithStableCodes()
    {
        var original = CodecTestData.Fingerprint();
        var view = original.Views[0];
        using (new AssertionScope())
        {
            Incits378Codec
                .Write(original with { Views = [view with { ViewNumber = 16 }] })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("FingerView");
            Incits378Codec
                .Write(original with { Views = [view with { Minutiae = [new(3, 1, 1, 1, 1)] }] })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("Minutia");
            Incits378Codec
                .Write(original with { Views = default })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("FingerprintViews");
            Incits378Codec
                .Write(original with { Width = 0 })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("Geometry");
        }
    }
}
