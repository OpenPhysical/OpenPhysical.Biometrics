// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.NBIS;

[TestFixture]
internal sealed class FingerprintRegionSelectionTests
{
    [Test]
    public void BinaryImageRegionPreservesNativeFieldsAndExcludesOutsideMaskEdges()
    {
        var template = new FingerprintTemplate(
            128,
            128,
            [new(0, 128, 270, 9, 1), new(1, 127, 91, 75, 2), new(2, 1, 30, 85, 1)],
            "actual extractor descriptor"
        );
        var mask = new byte[128 * 128];
        mask[0] = 1;
        var result = FingerprintRegionSelection.Select(template, ImmutableArray.CreateRange(mask));
        result.IsSuccess.Should().BeTrue();
        result.Value.Minutiae.Should().Equal(template.Minutiae.Take(1));
        result.Value.Provenance.Should().Be(template.Provenance);
        template.Minutiae.Should().HaveCount(3);
    }

    [TestCase(127 * 128, 1)]
    [TestCase(128 * 128, 2)]
    public void InvalidMaskLengthOrNonbinaryDataFailsBeforeNativeWork(int length, byte value)
    {
        var template = new FingerprintTemplate(128, 128, [new(0, 128, 270, 9, 1)], "fixture");
        var result = FingerprintRegionSelection.Select(
            template,
            ImmutableArray.CreateRange(Enumerable.Repeat(value, length))
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FingerprintErrorCodes.InvalidRegion);
    }
}
