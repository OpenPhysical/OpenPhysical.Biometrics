namespace OpenPhysical.Biometrics.Infrastructure.Tests.Utilities;

using System.Collections.Immutable;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using Canonical = OpenPhysical.Biometrics.Formats.Faces;

[TestFixture]
internal sealed class CanonicalFacialAdapterTests
{
    [Test]
    public void CompatibilityRoundTripPreservesSixteenBitQualityAndFacialMetadata()
    {
        var encoded = Canonical.Incits385Codec.Write(Record());
        var parsed = Incits385Parser.ParseIncits385Record(encoded.ToImmutableArray());
        Assert.That(parsed.IsSuccess, Is.True, parsed.IsFailure ? parsed.Error : string.Empty);
        Assert.That(parsed.Value.ImageInfo.Quality, Is.EqualTo(ushort.MaxValue));
        Assert.That(parsed.Value.ImageInfo.ImageColorSpace, Is.EqualTo(2));
        var written = new PivFacialFormatHandler().SerializeBiometricData(parsed.Value);
        Assert.That(written.IsSuccess, Is.True);
        Assert.That(written.Value, Is.EqualTo(encoded));
    }

    [Test]
    public void HandlerReturnsStructuralFailureForMalformedInput()
    {
        var handler = new PivFacialFormatHandler();
        Assert.That(
            handler.ValidateBiometricData(ImmutableArray.Create<byte>(1, 2, 3)).IsFailure,
            Is.True
        );
    }

    [Test]
    public void HandlerReturnsImageSignatureFailureForWellFramedWrongEncoding()
    {
        var record = Record();
        record = record with { Images = [record.Images[0] with { ImageData = [1, 2, 3, 4] }] };
        var encoded = Canonical.Incits385Codec.Write(record).ToImmutableArray();
        Assert.That(new PivFacialFormatHandler().ValidateBiometricData(encoded).IsFailure, Is.True);
    }

    private static Canonical.FaceRecord Record() =>
        new([
            new(
                new(0, 0, 0, 0, 1, 0, 0),
                [],
                new(1, 1, 480, 640, 2, 2, 1, ushort.MaxValue),
                ImmutableArray.Create<byte>(0xff, 0x4f, 0xff, 0x51)
            ),
        ]);
}
