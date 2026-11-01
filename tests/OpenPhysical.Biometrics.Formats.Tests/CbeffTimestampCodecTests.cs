using AwesomeAssertions;
using AwesomeAssertions.Execution;
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
        encoded.Should().Equal(new byte[] { 20, 5, 12, 15, 17, 35, 30, 90 });
        var decoded = CbeffTimestampCodec.Decode(encoded);
        encoded[2] = 1;
        decoded.Should().Be(instant.ToUniversalTime());
        CbeffTimestampCodec.Encode(instant)[2].Should().Be(12);
    }

    [TestCase(new byte[] { 20, 5, 2, 30, 17, 35, 30, 90 })]
    [TestCase(new byte[] { 20, 5, 12, 15, 17, 35, 30, 0 })]
    [TestCase(new byte[] { 20, 100, 12, 15, 17, 35, 30, 90 })]
    [TestCase(new byte[] { 20, 5, 12 })]
    public void MalformedTimestampsFailExplicitly(byte[] value) =>
        FluentActions
            .Invoking(() => CbeffTimestampCodec.Decode(value))
            .Should()
            .ThrowExactly<ArgumentException>();
}
