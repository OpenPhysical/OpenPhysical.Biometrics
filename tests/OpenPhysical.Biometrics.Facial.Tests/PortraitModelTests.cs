// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;

internal sealed class PortraitModelTests
{
    [Test]
    public void FullImageGeometryTracksThePinnedPythonReference()
    {
        using var reference = JsonDocument.Parse(TestFixtures.Bytes("glenn.reference.json"));
        using var service = TestFixtures.NewPreparer();
        var prepared = service.Prepare(TestFixtures.Bytes("glenn.jpg"));
        prepared.Should().Succeed();
        var actual = prepared.Value.Faces.Single();
        var expected = reference.RootElement.GetProperty("landmarks").EnumerateArray().ToArray();
        var differences = actual
            .Landmarks.Select(
                (point, i) =>
                    Math.Sqrt(
                        Math.Pow(point.X - expected[i][0].GetDouble(), 2)
                            + Math.Pow(point.Y - expected[i][1].GetDouble(), 2)
                    )
            )
            .ToArray();
        // Skia and OpenCV decode/resample independently; this bound is on source pixels.
        differences.Max().Should().BeLessThan(2);
    }

    [Test]
    public void FaceFeaturesAreDerivedFromTheCanonicalMesh()
    {
        var (face, _) = TestFixtures.Geometry();
        var features = face.Features;
        features.HasValue.Should().BeTrue();
        features.Value.NoseBridge.Should().Be(new PortraitPoint(600, 670));
        (face with { Landmarks = face.Landmarks.RemoveAt(0) }).Features.HasValue.Should().BeFalse();
    }
}
