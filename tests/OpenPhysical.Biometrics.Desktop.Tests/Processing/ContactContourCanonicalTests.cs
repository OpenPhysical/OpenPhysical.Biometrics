// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using NativeTemplate = OpenPhysical.Biometrics.NBIS.FingerprintTemplate;

[TestFixture]
internal sealed class ContactContourCanonicalTests
{
    [Test]
    public void IdenticalContactPreservesBytesWhileFilledHoleAdmissionIsExplicitlyDifferent()
    {
        var source = Fixture();
        var native = Record(source).ToMatchingTemplate(FingerPosition.RightIndex, 0).Value;
        var contact = Ring();
        var contour = Fs50ContactContourGeometry
            .Create(contact, contact, Fs50ContactContourPolicy.Default, default)
            .Value;
        var baseline = Select(source, native, contact);
        var sameContact = Select(source, native, contour.ContactMask.ToArray());
        Incits378Codec
            .Write(Record(sameContact))
            .Value.Should()
            .Equal(Incits378Codec.Write(Record(baseline)).Value);
        var filled = Select(
            source,
            native,
            contour.ToProcessingRegion(false).Value.Mask.Inner.ToArray()
        );
        baseline.View.Minutiae.Should().HaveCount(2);
        filled.View.Minutiae.Should().HaveCount(3);
        filled.View.Minutiae.Should().Contain(p => p.X == 380 && p.Y == 300 && p.Quality == 6);
        filled.View.Minutiae.Should().NotContain(p => p.X == 290 && p.Y == 190);
        contour.FilledHoleMask[300 * 800 + 380].Should().Be(1);
        contour.UncertainMask[300 * 800 + 380].Should().Be(1);
        contour.ContextMask[190 * 800 + 290].Should().Be(1);
    }

    private static CapturedFingerprint Fixture()
    {
        var capture = StorageFixtures.Capture(width: 800, height: 750);
        return capture with
        {
            View = capture.View with
            {
                Minutiae =
                [
                    new(1, 300, 250, 17, 80),
                    new(2, 450, 390, 45, 40),
                    new(1, 380, 300, 20, 6),
                    new(1, 290, 190, 70, 99),
                ],
            },
        };
    }

    private static byte[] Ring()
    {
        var mask = new byte[800 * 750];
        for (var y = 200; y < 400; y++)
        for (var x = 300; x < 460; x++)
            if (x < 310 || x >= 450 || y < 210 || y >= 390)
                mask[y * 800 + x] = 1;
        return mask;
    }

    private static CapturedFingerprint Select(
        CapturedFingerprint source,
        NativeTemplate native,
        byte[] mask
    )
    {
        var selected = FingerprintRegionSelection.Select(native, [.. mask]).Value;
        return source with
        {
            View = FingerprintViewFactory
                .Create(
                    selected,
                    source.Quality,
                    FingerPosition.RightIndex,
                    0,
                    FingerprintImpression.PlainLiveScan
                )
                .Value,
        };
    }

    private static FingerprintRecord Record(CapturedFingerprint source) =>
        UnsignedTemplateService.Create([source], new(0x0103, 0, 0)).Value;
}
