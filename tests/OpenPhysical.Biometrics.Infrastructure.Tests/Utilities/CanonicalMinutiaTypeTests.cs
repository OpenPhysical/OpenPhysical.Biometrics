namespace OpenPhysical.Biometrics.Infrastructure.Tests.Utilities;

using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using Canonical = OpenPhysical.Biometrics.Formats.Fingerprints;
using Legacy = OpenPhysical.Domains.Cbeff.Formats.Incits378;

[TestFixture]
internal sealed class CanonicalMinutiaTypeTests
{
    [TestCase(0, Legacy.MinutiaType.Other)]
    [TestCase(1, Legacy.MinutiaType.RidgeEnding)]
    [TestCase(2, Legacy.MinutiaType.Bifurcation)]
    public void CompatibilityProjectionPreservesEachWireType(byte type, Legacy.MinutiaType expected)
    {
        var record = new Canonical.FingerprintRecord(
            1,
            2,
            0x8001,
            128,
            128,
            197,
            197,
            [new(2, 0, 0, 80, [new(type, 12, 20, 45, 80)], [])]
        );
        var encoded = Canonical.Incits378Codec.Write(record);
        var parsed = Incits378Parser.ParseIncits378Record(encoded.ToImmutableArray());
        Assert.That(parsed.IsSuccess, Is.True, parsed.IsFailure ? parsed.Error : string.Empty);
        Assert.That(parsed.Value.ViewRecords[0].Minutiae[0].MinutiaType, Is.EqualTo(expected));
        var handler = new PivFingerprintFormatHandler(
            NullLogger<PivFingerprintFormatHandler>.Instance
        );
        var roundTrip = handler.SerializeBiometricData(parsed.Value);
        Assert.That(roundTrip.IsSuccess, Is.True);
        Assert.That(roundTrip.Value, Is.EqualTo(encoded));
    }
}
