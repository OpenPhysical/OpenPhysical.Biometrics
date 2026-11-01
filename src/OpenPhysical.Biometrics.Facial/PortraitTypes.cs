// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;

/// <summary>A position in orientation-corrected source pixels, measured from the upper-left corner.</summary>
/// <param name="X">Horizontal position, increasing to the right.</param>
/// <param name="Y">Vertical position, increasing downwards.</param>
public readonly record struct PortraitPoint(double X, double Y);

/// <summary>A MediaPipe landmark in source-pixel units.</summary>
/// <param name="X">Horizontal position.</param>
/// <param name="Y">Vertical position.</param>
/// <param name="Z">Relative depth, meaningful only within the detected face.</param>
public readonly record struct FaceMeshPoint(double X, double Y, double Z);

/// <summary>An immutable detection with the canonical MediaPipe 478-point topology.</summary>
/// <param name="Index">Stable zero-based index, ordered by horizontal center then vertical center.</param>
/// <param name="Confidence">BlazeFace detection probability.</param>
/// <param name="Presence">Landmark-network face-presence probability.</param>
/// <param name="Landmarks">The 478 mesh points in canonical model order.</param>
/// <param name="ViewerLeftEar">Detector estimate of the ear attachment on the viewer's left.</param>
/// <param name="ViewerRightEar">Detector estimate of the ear attachment on the viewer's right.</param>
public sealed record PortraitFace(
    int Index,
    double Confidence,
    double Presence,
    ImmutableArray<FaceMeshPoint> Landmarks,
    PortraitPoint ViewerLeftEar,
    PortraitPoint ViewerRightEar
)
{
    /// <summary>Gets anatomical features derived from the MediaPipe mesh topology.</summary>
    public PortraitFeatures Features => PortraitFeatures.FromMediaPipe(Landmarks);
}

/// <summary>Operator-reviewed geometry tied to the exact encoded source.</summary>
/// <param name="Version">Schema version, currently one.</param>
/// <param name="SourceSha256">SHA-256 of the original encoded image.</param>
/// <param name="Width">Width after applying EXIF orientation.</param>
/// <param name="Height">Height after applying EXIF orientation.</param>
/// <param name="FaceIndex">Selected detection in stable display order.</param>
/// <param name="Crown">Reviewed top of the hair.</param>
/// <param name="Chin">Reviewed bottom of the chin, or null to use the mesh.</param>
/// <param name="ViewerLeftEar">Reviewed left ear attachment.</param>
/// <param name="ViewerRightEar">Reviewed right ear attachment.</param>
public sealed record ReviewedPortraitGeometry(
    int Version,
    string SourceSha256,
    int Width,
    int Height,
    int FaceIndex,
    PortraitPoint? Crown = null,
    PortraitPoint? Chin = null,
    PortraitPoint? ViewerLeftEar = null,
    PortraitPoint? ViewerRightEar = null
);

/// <summary>Expected outcomes of portrait preparation.</summary>
public enum PortraitStatus
{
    /// <summary>The source supports the selected portrait geometry.</summary>
    Prepared,

    /// <summary>No face passed detection and landmark presence checks.</summary>
    NoFace,

    /// <summary>The caller must select one of several faces.</summary>
    FaceSelectionRequired,

    /// <summary>Geometry requires reviewed measurements or a new capture.</summary>
    ReviewRequired,
}

/// <summary>A stable diagnostic for portrait preparation.</summary>
/// <param name="Code">Machine-readable diagnostic code.</param>
/// <param name="Message">Plain-language explanation.</param>
public sealed record PortraitFinding(string Code, string Message);

/// <summary>A uniform source-to-output affine transform.</summary>
/// <param name="A">Scaled cosine of the correction angle.</param>
/// <param name="B">Scaled sine of the correction angle.</param>
/// <param name="TranslateX">Horizontal translation in output pixels.</param>
/// <param name="TranslateY">Vertical translation in output pixels.</param>
public sealed record PortraitTransform(double A, double B, double TranslateX, double TranslateY)
{
    /// <summary>Maps a source position into the output portrait.</summary>
    /// <param name="point">Source position.</param>
    /// <returns>Output position.</returns>
    public PortraitPoint Map(PortraitPoint point) =>
        new(A * point.X + B * point.Y + TranslateX, -B * point.X + A * point.Y + TranslateY);

    /// <summary>Maps an output position into the source image.</summary>
    /// <param name="point">Output position.</param>
    /// <returns>Source position.</returns>
    public PortraitPoint Inverse(PortraitPoint point)
    {
        var norm = A * A + B * B;
        if (!double.IsFinite(norm) || norm <= 0)
            throw new InvalidOperationException("Transform is singular.");
        var x = point.X - TranslateX;
        var y = point.Y - TranslateY;
        return new((A * x - B * y) / norm, (B * x + A * y) / norm);
    }
}

/// <summary>Measurements supporting the selected crop.</summary>
/// <param name="HeadWidth">Ear-attachment separation in output pixels.</param>
/// <param name="HeadHeight">Crown-to-chin extent in output pixels.</param>
/// <param name="EyeHeightFraction">Eye position measured down from the output top.</param>
/// <param name="ReviewedHeadBoundary">Whether crown and both ear attachments were supplied by an operator.</param>
public sealed record PortraitMeasurements(
    double HeadWidth,
    double HeadHeight,
    double EyeHeightFraction,
    bool ReviewedHeadBoundary
)
{
    /// <summary>Gets how the crown boundary was established.</summary>
    public PortraitMeasurementOrigin CrownOrigin { get; init; } =
        PortraitMeasurementOrigin.Estimated;

    /// <summary>Gets how the left ear attachment was established.</summary>
    public PortraitMeasurementOrigin ViewerLeftEarOrigin { get; init; } =
        PortraitMeasurementOrigin.Estimated;

    /// <summary>Gets how the right ear attachment was established.</summary>
    public PortraitMeasurementOrigin ViewerRightEarOrigin { get; init; } =
        PortraitMeasurementOrigin.Estimated;

    /// <summary>Gets how the chin boundary was established.</summary>
    public PortraitMeasurementOrigin ChinOrigin { get; init; } = PortraitMeasurementOrigin.Mesh;
}

/// <summary>Distinguishes operator review from automatic anatomical measurements.</summary>
public enum PortraitMeasurementOrigin
{
    /// <summary>An automatic estimate of a head boundary.</summary>
    Estimated,

    /// <summary>A position inferred from the canonical mesh.</summary>
    Mesh,

    /// <summary>A coordinate supplied in the reviewed geometry file.</summary>
    Reviewed,
}

/// <summary>An owned portrait preparation result with immutable pixels and findings.</summary>
/// <param name="Status">Preparation outcome.</param>
/// <param name="SourceSha256">Hash of the encoded source.</param>
/// <param name="SourceWidth">Oriented source width.</param>
/// <param name="SourceHeight">Oriented source height.</param>
/// <param name="Faces">Detections in stable order.</param>
/// <param name="Transform">Source-to-output mapping when prepared.</param>
/// <param name="Measurements">Crop measurements when prepared.</param>
/// <param name="RgbPixels">Owned 480 by 640 sRGB pixels, or empty when review is required.</param>
/// <param name="Findings">Structural and review findings.</param>
public sealed record PortraitPreparationResult(
    PortraitStatus Status,
    string SourceSha256,
    int SourceWidth,
    int SourceHeight,
    ImmutableArray<PortraitFace> Faces,
    PortraitTransform? Transform,
    PortraitMeasurements? Measurements,
    ImmutableArray<byte> RgbPixels,
    ImmutableArray<PortraitFinding> Findings
)
{
    /// <summary>Gets the pinned model source revision used by this implementation.</summary>
    public string ModelRevision => PortraitProvenance.ModelRevision;

    /// <summary>Gets the versioned crop policy used by this implementation.</summary>
    public string GeometryPolicy => PortraitProvenance.GeometryPolicy;
}

/// <summary>Named anatomical features in orientation-corrected source pixels.</summary>
/// <param name="ViewerLeftEye">Midpoint of the left eye corners as viewed in the image.</param>
/// <param name="ViewerRightEye">Midpoint of the right eye corners as viewed in the image.</param>
/// <param name="NoseBridge">Nose bridge used for horizontal centering.</param>
/// <param name="MouthCenter">Midpoint of the inner upper and lower lip.</param>
/// <param name="Chin">Bottom of the mesh chin.</param>
/// <param name="Forehead">Upper mesh contour, distinct from the crown of the hair.</param>
public sealed record PortraitFeatures(
    PortraitPoint ViewerLeftEye,
    PortraitPoint ViewerRightEye,
    PortraitPoint NoseBridge,
    PortraitPoint MouthCenter,
    PortraitPoint Chin,
    PortraitPoint Forehead
)
{
    internal static PortraitFeatures FromMediaPipe(ImmutableArray<FaceMeshPoint> points)
    {
        if (points.IsDefault || points.Length != 478)
            throw new ArgumentException(
                "MediaPipe geometry requires exactly 478 landmarks.",
                nameof(points)
            );
        PortraitPoint At(int index) => new(points[index].X, points[index].Y);
        PortraitPoint Mid(int a, int b) =>
            new((points[a].X + points[b].X) / 2, (points[a].Y + points[b].Y) / 2);
        // These are anatomical correspondences in Google's canonical face mesh.
        return new(Mid(33, 133), Mid(362, 263), At(168), Mid(13, 14), At(152), At(10));
    }
}

/// <summary>Identifies the model artifacts and geometry policy used for preparation.</summary>
public static class PortraitProvenance
{
    /// <summary>The immutable source revision of the published ONNX conversion.</summary>
    public const string ModelRevision = "3bdf1e0d100a4c709766c051a0c9e9cdab1fed62";

    /// <summary>The versioned application crop policy implementing Full Frontal geometry.</summary>
    public const string GeometryPolicy = "piv-full-frontal-480x640-v1";
}
