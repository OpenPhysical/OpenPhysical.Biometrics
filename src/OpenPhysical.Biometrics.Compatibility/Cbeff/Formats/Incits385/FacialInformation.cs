namespace OpenPhysical.Domains.Cbeff.Formats.Incits385;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;

/// <summary>
/// Represents the facial information block in an INCITS 385 record.
/// </summary>
public sealed record FacialInformation
{
    /// <summary>
    /// Gets the face image block length in bytes.
    /// </summary>
    public uint FaceImageBlockLength { get; }

    /// <summary>
    /// Gets the number of feature points.
    /// </summary>
    public ushort NumberOfFeaturePoints { get; }

    /// <summary>
    /// Gets the gender code (0 = unspecified, 1 = male, 2 = female).
    /// </summary>
    public byte Gender { get; }

    /// <summary>
    /// Gets the eye color code.
    /// </summary>
    public byte EyeColor { get; }

    /// <summary>
    /// Gets the hair color code.
    /// </summary>
    public byte HairColor { get; }

    /// <summary>
    /// Gets the feature mask (3 bytes).
    /// </summary>
    public uint FeatureMask { get; }

    /// <summary>
    /// Gets the expression code (should be 1 for neutral).
    /// </summary>
    public ushort Expression { get; }

    /// <summary>
    /// Gets the pose angle (3 bytes, should be 0 for frontal).
    /// </summary>
    public uint PoseAngle { get; }

    /// <summary>
    /// Gets the pose angle uncertainty (3 bytes).
    /// </summary>
    public uint PoseAngleUncertainty { get; }

    private FacialInformation(
        uint faceImageBlockLength,
        ushort numberOfFeaturePoints,
        byte gender,
        byte eyeColor,
        byte hairColor,
        uint featureMask,
        ushort expression,
        uint poseAngle,
        uint poseAngleUncertainty
    )
    {
        FaceImageBlockLength = faceImageBlockLength;
        NumberOfFeaturePoints = numberOfFeaturePoints;
        Gender = gender;
        EyeColor = eyeColor;
        HairColor = hairColor;
        FeatureMask = featureMask;
        Expression = expression;
        PoseAngle = poseAngle;
        PoseAngleUncertainty = poseAngleUncertainty;
    }

    /// <summary>
    /// Creates a new facial information block with validation.
    /// </summary>
    public static Result<FacialInformation> Create(
        uint faceImageBlockLength,
        ushort numberOfFeaturePoints,
        byte gender,
        byte eyeColor,
        byte hairColor,
        uint featureMask,
        ushort expression,
        uint poseAngle,
        uint poseAngleUncertainty
    )
    {
        if (faceImageBlockLength == 0)
            return Result.Failure<FacialInformation>("Face image block length cannot be zero");

        if (featureMask > 0xFFFFFF)
            return Result.Failure<FacialInformation>("Feature mask must fit in 3 bytes");

        if (poseAngle > 0xFFFFFF)
            return Result.Failure<FacialInformation>("Pose angle must fit in 3 bytes");

        if (poseAngleUncertainty > 0xFFFFFF)
            return Result.Failure<FacialInformation>("Pose angle uncertainty must fit in 3 bytes");

        return Result.Success(
            new FacialInformation(
                faceImageBlockLength,
                numberOfFeaturePoints,
                gender,
                eyeColor,
                hairColor,
                featureMask,
                expression,
                poseAngle,
                poseAngleUncertainty
            )
        );
    }
}
