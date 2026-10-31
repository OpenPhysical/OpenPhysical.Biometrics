using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;

namespace OpenPhysical.Biometrics.Formats.Tests;

[TestFixture]
internal sealed class CbeffTimestampCodecTests
{
    [Test]
    public void IntegerPairsUseUtcAndCallerOwnsEncodedBuffers()
    {
        var instant = new DateTimeOffset(2005, 12, 15, 18, 35, 30, TimeSpan.FromHours(1));
        var encoded = CbeffTimestampCodec.Encode(instant);
        Assert.That(encoded, Is.EqualTo(new byte[] { 20, 5, 12, 15, 17, 35, 30, 90 }));
        var decoded = CbeffTimestampCodec.Decode(encoded);
        encoded[2] = 1;
        Assert.That(decoded, Is.EqualTo(instant.ToUniversalTime()));
        Assert.That(CbeffTimestampCodec.Encode(instant)[2], Is.EqualTo(12));
    }

    [TestCase(new byte[] { 20, 5, 2, 30, 17, 35, 30, 90 })]
    [TestCase(new byte[] { 20, 5, 12, 15, 17, 35, 30, 0 })]
    [TestCase(new byte[] { 20, 100, 12, 15, 17, 35, 30, 90 })]
    [TestCase(new byte[] { 20, 5, 12 })]
    public void MalformedTimestampsFailExplicitly(byte[] value) =>
        Assert.Throws<ArgumentException>(() => CbeffTimestampCodec.Decode(value));
}
