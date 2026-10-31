namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using System.Collections.Immutable;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using Canonical = OpenPhysical.Biometrics.Formats.Faces;

// Legacy inspection presents one face. Canonical records retain the complete
// multi-image format; the compatibility boundary requires an explicit single face.
internal static class LegacyFacialProjection
{
    internal static FacialImageRecord FromCanonical(Canonical.FaceRecord record)
    {
        if (record.Images.Length != 1)
            throw new ArgumentException(
                "Select one facial image for the compatibility model.",
                nameof(record)
            );
        var encoded = Canonical.Incits385Codec.Write(record);
        var face = record.Images[0];
        var info = face.Information;
        var image = face.ImageInformation;
        return FacialImageRecord
            .Create(
                FacialRecordHeader.Create("FAC\0", "010\0", (uint)encoded.Length, 1).Value,
                FacialInformation
                    .Create(
                        (uint)(encoded.Length - 14),
                        checked((ushort)face.FeaturePoints.Length),
                        info.Gender,
                        info.EyeColor,
                        info.HairColor,
                        info.FeatureMask,
                        info.Expression,
                        info.PoseAngle,
                        info.PoseUncertainty
                    )
                    .Value,
                face.FeaturePoints.Select(point =>
                        FeaturePoint.Create(point.Type, point.Identifier, point.X, point.Y, 0).Value
                    )
                    .ToImmutableArray(),
                ImageInformation
                    .Create(
                        image.FaceImageType,
                        image.ImageDataType,
                        image.Width,
                        image.Height,
                        image.ColorSpace,
                        image.SourceType,
                        image.DeviceType,
                        image.Quality
                    )
                    .Value,
                face.ImageData
            )
            .Value;
    }

    internal static Canonical.FaceRecord ToCanonical(FacialImageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (
            record.Header.NumberOfFaces != 1
            || record.FeaturePoints.Any(point => point.Reserved != 0)
        )
            throw new ArgumentException(
                "The compatibility record requires one face and zero reserved feature-point fields.",
                nameof(record)
            );
        var info = record.FacialInfo;
        var image = record.ImageInfo;
        return new Canonical.FaceRecord([
            new(
                new(
                    info.Gender,
                    info.EyeColor,
                    info.HairColor,
                    info.FeatureMask,
                    info.Expression,
                    info.PoseAngle,
                    info.PoseAngleUncertainty
                ),
                record
                    .FeaturePoints.Select(point => new Canonical.FaceFeaturePoint(
                        point.FeatureType,
                        point.FeaturePointId,
                        point.HorizontalPosition,
                        point.VerticalPosition
                    ))
                    .ToImmutableArray(),
                new(
                    image.FacialImageType,
                    image.ImageDataType,
                    image.Width,
                    image.Height,
                    image.ImageColorSpace,
                    image.SourceType,
                    image.DeviceType,
                    image.Quality
                ),
                record.ImageData
            ),
        ]);
    }
}
