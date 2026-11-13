// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Packages;

internal static class PackageReviewFixtures
{
    internal static async Task<(
        EnrollmentOperatorContext Context,
        BiometricPackage Package
    )> Fingerprints()
    {
        var storage = new ControlledExportStorage();
        storage.Completed.SetResult(Result.Success<string, DesktopError>("memory-only.opbio"));
        var context = new EnrollmentOperatorContext(storage, EnrollmentTestSuggestions.IndexPair());
        context.Export.Enrollment.Person.Name = "Example person";
        context.Export.Enrollment.Fingerprints.TwoFingers = false;
        (await context.AcceptAsync(context.NativeCapture())).Should().Succeed();
        await context.Export.SaveCommand.ExecuteAsync(default);
        storage.Package.HasValue.Should().BeTrue();
        return (context, storage.Package.Value);
    }

    internal static BiometricPackage Face(ushort expression = 1)
    {
        using var image = RgbImageDecoder
            .Load(
                Path.Combine(
                    NUnit.Framework.TestContext.CurrentContext.TestDirectory,
                    "Fixtures",
                    "face-bush.jpg"
                )
            )
            .Value;
        var rgb = image.CopyRgbBytes().Value;
        try
        {
            var encoded = PortraitEncoding.EncodeJpeg2000(rgb, image.Width, image.Height).Value;
            return FacePackage(encoded, image.Width, image.Height, expression);
        }
        finally
        {
            Array.Clear(rgb);
        }
    }

    private static BiometricPackage FacePackage(
        byte[] encoded,
        int width,
        int height,
        ushort expression
    )
    {
        var record = new FaceRecord([
            new(
                new(0, 0, 0, 0, expression, 0, 0),
                [],
                new(1, 1, (ushort)width, (ushort)height, 1, 2, 0, 0),
                ImmutableArray.Create(encoded)
            ),
        ]);
        return BiometricPackage
            .Create(
                "example-person",
                "Example person",
                Maybe<string>.None,
                "{\"Sections\":[\"face\"]}"u8,
                [
                    PackageArtifact
                        .Create(
                            "face.bin",
                            Incits385Codec.Write(record).Value,
                            "face",
                            "ANSI/INCITS 385-2004"
                        )
                        .Value,
                ]
            )
            .Value;
    }

    internal static BiometricPackage WithCompanionDefect(BiometricPackage original, string defect)
    {
        var files = original.Artifacts;
        var manifest = JsonNode.Parse(
            files.Single(value => value.Name == "fingerprints.json").Bytes.AsSpan()
        )!;
        var image = manifest["Images"]![0]!;
        var name = image["FileName"]!.GetValue<string>();
        if (defect == "missing")
            files = [.. files.Where(value => value.Name != name)];
        else if (defect == "hash")
            image["Sha256"] = new string('0', 64);
        else if (defect == "pixels")
            image["PixelSha256"] = new string('0', 64);
        else
            image["Width"] = 17;
        var manifestBytes = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        files =
        [
            .. files.Select(value =>
                value.Name == "fingerprints.json"
                    ? PackageArtifact
                        .Create(value.Name, manifestBytes, value.Role, value.Format)
                        .Value
                    : value
            ),
        ];
        return BiometricPackage
            .Create(
                original.PersonId,
                original.Label,
                original.ExternalId,
                original.EvidenceJson.AsSpan(),
                files
            )
            .Value;
    }
}
