// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

internal static class RidgeBoundaryControls
{
    internal static IEnumerable<RidgeBoundaryControl> All() =>
        Positive().Concat(Negative()).Concat(PublicDiagnostics());

    private static IEnumerable<RidgeBoundaryControl> Positive()
    {
        var empty = Fs50EvidenceFrameFixture.Empty();
        var actual = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        yield return new("public-positive", "qualified", actual, empty);
        yield return new("public-primary-only", "qualified", actual, empty, true);
        yield return new(
            "faint-only-exposure",
            "exposure-only",
            HysteresisFingerFixture.WeakOnly(242),
            empty
        );
        yield return new(
            "dark-enough-weak-only",
            "qualified",
            HysteresisFingerFixture.WeakOnly(215),
            empty
        );
        yield return new("faint-annulus", "qualified", HysteresisFingerFixture.Annulus(), empty);
        yield return new("closed-weak-core", "qualified", WeakCore(), empty);
    }

    private static IEnumerable<RidgeBoundaryControl> Negative()
    {
        var empty = Fs50EvidenceFrameFixture.Empty();
        yield return new("strong-smear", "rejected", HysteresisFingerFixture.StrongSmear(), empty);
        yield return new("weak-dots", "rejected", HysteresisFingerFixture.WeakDots(), empty);
        yield return new(
            "weak-two-finger-bridge",
            "rejected",
            HysteresisFingerFixture.WeakBridge(),
            empty
        );
        yield return new(
            "majority-strong-smear",
            "rejected",
            HysteresisFingerFixture.AttachedStrongSmear(false),
            empty
        );
        yield return new(
            "far-support-smear",
            "rejected",
            HysteresisFingerFixture.AttachedStrongSmear(true),
            empty
        );
        yield return ThinStatic();
    }

    private static IEnumerable<RidgeBoundaryControl> PublicDiagnostics()
    {
        var empty = Fs50EvidenceFrameFixture.Empty();
        foreach (var name in ContactEvidenceFixtures.PublicNames())
            yield return new(
                "diagnostic-" + name,
                "diagnostic",
                ContactEvidenceFixtures.PublicFinger(name),
                empty
            );
    }

    private static byte[] WeakCore()
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(pixels, 280, 210, 240, 320);
        for (var row = 274; row < 466; row++)
            pixels.AsSpan(row * 800 + 344, 112).Fill(255);
        return pixels;
    }

    private static RidgeBoundaryControl ThinStatic()
    {
        var reference = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(reference, 650, 210, 16, 300);
        var pixels = reference.ToArray();
        _ = WholeContactRig.Ridges(pixels, 480, 200, 64, 96);
        _ = WholeContactRig.Ridges(pixels, 544, 210, 106, 16);
        return new("thin-static-appendage", "rejected", pixels, reference);
    }
}
