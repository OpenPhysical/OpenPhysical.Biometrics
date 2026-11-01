// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Faces;

[TestFixture]
internal sealed class FacialProfileTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void PivFacialMetadataRequiresFrontalPoseEncoding(bool angle)
    {
        var valid = ProfileTestData.Face();
        PivBiometricProfile.Validate(valid).Should().BeEmpty();
        var image = valid.Images[0];
        var altered = image with
        {
            Information = angle
                ? image.Information with
                {
                    PoseAngle = 1,
                }
                : image.Information with
                {
                    PoseUncertainty = 1,
                },
        };
        PivBiometricProfile
            .Validate(new FaceRecord([altered]))
            .Select(finding => finding.Code)
            .Should()
            .Contain(angle ? "FrontalPose" : "PoseUncertainty");
    }

    [TestCase("FullFrontalImage")]
    [TestCase("ImageEncoding")]
    [TestCase("SrgbColorSpace")]
    [TestCase("CaptureSource")]
    [TestCase("ImageDimensions")]
    [TestCase("ImageQuality")]
    [TestCase("NeutralExpression")]
    [TestCase("ImageDataRequired")]
    public void FacialMetadataChecksAcquisitionFieldsAndRequiredImageContent(string code) =>
        PivBiometricProfile
            .Validate(new FaceRecord([Corrupt(ProfileTestData.Face().Images[0], code)]))
            .Select(finding => finding.Code)
            .Should()
            .Contain(code);

    // Alters exactly the field that the named profile requirement checks.
    private static FaceImage Corrupt(FaceImage image, string code)
    {
        var info = image.ImageInformation;
        return code switch
        {
            "FullFrontalImage" => image with { ImageInformation = info with { FaceImageType = 0 } },
            "ImageEncoding" => image with { ImageInformation = info with { ImageDataType = 2 } },
            "SrgbColorSpace" => image with { ImageInformation = info with { ColorSpace = 3 } },
            "CaptureSource" => image with { ImageInformation = info with { SourceType = 0 } },
            "ImageDimensions" => image with { ImageInformation = info with { Width = 420 } },
            "ImageQuality" => image with { ImageInformation = info with { Quality = 101 } },
            "NeutralExpression" => image with
            {
                Information = image.Information with { Expression = 0 },
            },
            _ => image with { ImageData = [] },
        };
    }
}
