using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Standards.Incits385;
using OpenPhysical.Biometrics.Infrastructure.Standards.Incits385;
using OpenPhysical.Domains.Biometrics;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Services;

[TestFixture]
internal sealed class CanonicalFacialWriterTests
{
    [TestCase(ImageFormat.Jpeg)]
    [TestCase(ImageFormat.Jpeg2000)]
    public void LegacyWriterPreservesSrgbColorAndTheCompleteQualityField(ImageFormat format)
    {
        var metadata = Incits385Metadata.Default with { Quality = 75 };
        var result = new Incits385WriterService().CreateFacialRecord(
            new byte[] { 1, 2, 3 },
            format,
            ImageDimensions.Create(640, 480).Value,
            metadata
        );
        Assert.That(result.IsSuccess, Is.True);
        // Read the standardized image-information fields independently of the
        // writer. Both formats carry the same original sRGB capture metadata.
        Assert.Multiple(() =>
        {
            Assert.That(result.Value[35], Is.EqualTo((byte)format));
            Assert.That(result.Value[40], Is.EqualTo(1));
            Assert.That(result.Value[44], Is.Zero);
            Assert.That(result.Value[45], Is.EqualTo(75));
        });
    }
}
