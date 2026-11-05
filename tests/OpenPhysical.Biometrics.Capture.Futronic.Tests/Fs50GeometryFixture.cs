// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Buffers.Binary;
using System.Text.Json;
using NUnit.Framework;

/// <summary>Shared synthetic calibration fixture; no captured fingerprint pixels.</summary>
internal static class Fs50GeometryFixture
{
    internal static JsonDocument Load() =>
        JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "Fixtures",
                    "fs50-geometry-v1.json"
                )
            )
        );

    internal static byte[] Descriptor(JsonElement fixture) =>
        Convert.FromHexString(fixture.GetProperty("descriptorHex").ToString());

    internal static byte[] Controls(JsonElement fixture) =>
        Convert.FromHexString(fixture.GetProperty("controlsHex").ToString());

    internal static CSharpFunctionalExtensions.Result<Fs50Calibration, CaptureError> Parse(
        JsonElement fixture
    ) => Fs50Calibration.Parse(Descriptor(fixture), Controls(fixture), CalibrationBytes(fixture));

    internal static byte[] CalibrationBytes(JsonElement fixture)
    {
        var bytes = new byte[4352];
        var i = 0;
        foreach (var coefficient in fixture.GetProperty("coefficients").EnumerateArray())
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(16 + (4 * i)),
                coefficient.GetInt32()
            );
            i++;
        }

        return bytes;
    }
}
