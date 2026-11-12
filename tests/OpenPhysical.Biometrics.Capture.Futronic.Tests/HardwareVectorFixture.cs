// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.IO.Compression;
using System.Text.Json;
using CSharpFunctionalExtensions;
using NUnit.Framework;

internal static class HardwareVectorFixture
{
    internal static string Directory =>
        Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            "hardware-fs50-20261110"
        );

    internal static JsonDocument Manifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, "hardware-vectors.json")));

    internal static byte[] Metadata(string name) =>
        File.ReadAllBytes(Path.Combine(Directory, name));

    internal static Fs50GeometryMap Map() =>
        Fs50Calibration
            .Parse(
                Metadata("descriptor.bin"),
                Metadata("controls.bin"),
                Metadata("calibration.bin")
            )
            .Bind(Fs50GeometryMap.Create)
            .Value;

    internal static byte[] Image(int exposure, string stage)
    {
        using var source = File.OpenRead(
            Path.Combine(Directory, $"empty-{exposure}.{stage}.gray8.gz")
        );
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
