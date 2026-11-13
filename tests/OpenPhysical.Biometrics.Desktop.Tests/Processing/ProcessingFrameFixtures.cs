// SPDX-License-Identifier: GPL-2.0-or-later
using System.IO.Compression;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class ProcessingFrameFixtures
{
    internal static byte[] PaleRidgeContact()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var y = 150; y < 550; y++)
        for (var x = 250; x < 550; x++)
        {
            var ellipse = Math.Pow((x - 400) / 150.0, 2) + Math.Pow((y - 350) / 200.0, 2);
            if (ellipse <= 1)
                pixels[y * 800 + x] = x % 10 < 5 ? (byte)225 : (byte)245;
        }
        return pixels;
    }

    internal static byte[] PublicFinger()
    {
        var source = File.ReadAllBytes(
            Path.Combine(
                NUnit.Framework.TestContext.CurrentContext.TestDirectory,
                "Fixtures",
                "sd04-f0005_03.gray8"
            )
        );
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var row = 0; row < 512; row++)
            source.AsSpan(row * 512, 512).CopyTo(pixels.AsSpan(((row + 119) * 800) + 144, 512));
        return pixels;
    }

    internal static byte[] FaintResidue()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        uint state = 19;
        for (var y = 210; y < 530; y++)
        for (var x = 280; x < 520; x++)
        {
            state = state * 1664525 + 1013904223;
            pixels[y * 800 + x] = (byte)(235 + state % 21);
        }
        return pixels;
    }

    internal static byte[] EmptyHardwareFrame()
    {
        using var input = File.OpenRead(
            Path.Combine(
                NUnit.Framework.TestContext.CurrentContext.TestDirectory,
                "Fixtures",
                "fs50-empty.gray8.gz"
            )
        );
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    internal static CaptureFrame Frame(byte[] pixels)
    {
        var source = StorageFixtures.Capture(width: 800, height: 750).Frame;
        return CaptureFrame
            .Create(pixels, source.Sequence, source.CapturedAt, source.Provenance)
            .Value;
    }

    internal static CaptureFrame NormalizedFrame(byte[] pixels)
    {
        var device = NormalizedDevice();
        var geometry = CaptureGeometry.Create(800, 750).Value;
        var settings = CaptureSettings.Create(255, false, true, TimeSpan.FromSeconds(20)).Value;
        const string operation = "offline-identity-padding-test-boundary-v1";
        var digest = Convert.FromHexString(
            EvaluationDigest.Of(System.Text.Encoding.UTF8.GetBytes(operation))
        );
        var provenance = CaptureProvenance
            .Create(
                device,
                operation,
                geometry,
                geometry,
                settings,
                digest,
                operation,
                CaptureResolution
                    .Create(
                        CaptureResolution.EvidenceKind.Declared,
                        500,
                        500,
                        "Pinned public image source; unchanged physical scale"
                    )
                    .Value,
                CaptureQualification.Experimental
            )
            .Value;
        return CaptureFrame
            .Create(pixels, 42, DateTimeOffset.UnixEpoch.AddDays(1), provenance)
            .Value;
    }

    private static CaptureDeviceDescriptor NormalizedDevice() =>
        CaptureDeviceDescriptor
            .Create(
                "public-image-test-source",
                "Public corpus",
                "Offline normalized Gray8 fixture",
                Maybe<string>.None,
                CaptureCapabilities.Create(0, 255, true, true).Value
            )
            .Value;
}
