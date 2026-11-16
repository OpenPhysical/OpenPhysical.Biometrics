// SPDX-License-Identifier: Apache-2.0
// Google MediaPipe canonical coordinates and weights; see Notices/Google-Face-Geometry-Apache-2.0.txt.
namespace OpenPhysical.Biometrics.Facial;

using System.Globalization;
using System.Text;
using CSharpFunctionalExtensions;
using MathNet.Numerics.LinearAlgebra;

internal sealed record PoseCanonicalModel(Matrix<double> Points, Vector<double> Weights)
{
    internal static Result<PoseCanonicalModel, FacialError> Load() =>
        Maybe
            .From(
                typeof(PoseCanonicalModel).Assembly.GetManifestResourceStream(
                    "OpenPhysical.Biometrics.Facial.Pose.GoogleCanonicalFace.csv"
                )
            )
            .ToResult(FacialErrors.ModelMissing())
            .Bind(Read);

    private static Result<PoseCanonicalModel, FacialError> Read(Stream resource) =>
        FacialEdge.TryBind(
            () =>
            {
                using var stream = resource;
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var bytes = buffer.ToArray();
                if (Sha256.HexDigest(bytes) != PortraitPoseEstimator.CanonicalModelSha256)
                    return FacialErrors.ModelChecksum().Fail<PoseCanonicalModel>();
                var rows = Encoding
                    .UTF8.GetString(bytes)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !line.StartsWith('#'))
                    .Select(line =>
                        line.Split(',')
                            .Select(value => double.Parse(value, CultureInfo.InvariantCulture))
                            .ToArray()
                    )
                    .ToArray();
                if (
                    rows.Length != 468
                    || rows.Any(row => row.Length != 4 || row.Any(value => !double.IsFinite(value)))
                )
                    return FacialErrors.ModelChecksum().Fail<PoseCanonicalModel>();
                return new PoseCanonicalModel(
                    Matrix<double>.Build.Dense(3, rows.Length, (r, c) => rows[c][r]),
                    Vector<double>.Build.Dense(rows.Length, i => rows[i][3])
                ).Succeed();
            },
            FacialErrorCodes.ModelLoadFailed,
            "The packaged canonical face model could not be read."
        );
}
