// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;

/// <summary>Selects immutable native minutiae using an image-aligned binary region.</summary>
public static class FingerprintRegionSelection
{
    /// <summary>Retains points inside the region without changing coordinates, direction, quality or provenance.</summary>
    /// <param name="template">Native height-minus-row template.</param>
    /// <param name="region">Top-left-origin row-major zero/one mask, matching the template geometry.</param>
    /// <returns>The selected template, including an empty selection, or an invalid-region error.</returns>
    public static Result<FingerprintTemplate, FingerprintError> Select(
        FingerprintTemplate template,
        ImmutableArray<byte> region
    ) =>
        FingerprintChecks
            .Require(
                !region.IsDefault
                    && region.Length == (long)template.Width * template.Height
                    && region.All(value => value <= 1),
                FingerprintErrorCodes.InvalidRegion,
                "The binary region must match the native template geometry."
            )
            .Bind(() =>
                FingerprintEdge.Try(
                    () =>
                        new FingerprintTemplate(
                            template.Width,
                            template.Height,
                            template.Minutiae.Where(point =>
                                Inside(point, template.Width, template.Height, region)
                            ),
                            template.Provenance
                        ),
                    FingerprintErrorCodes.InvalidTemplate,
                    "Cannot select native fingerprint features."
                )
            );

    private static bool Inside(
        FingerprintMinutia point,
        int width,
        int height,
        ImmutableArray<byte> region
    )
    {
        var row = FingerprintGeometry.ToImageRow(point, height);
        return point.X >= 0
            && point.X < width
            && row >= 0
            && row < height
            && region[row * width + point.X] != 0;
    }
}
