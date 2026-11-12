// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateObservedContact
{
    internal static Fs50WholeContactRidgeEvidence From(Fs50ContactDiagnostics value) =>
        new(
            TemplateSha256.Of(value.ObservedContactMask.ToArray()),
            value.ObservedContactMask.Count(pixel => pixel != 0),
            value.RidgeExteriorArea,
            value.LargestRidgeExteriorArea
        );

    internal static bool Same(
        Fs50WholeContactRidgeEvidence left,
        Fs50WholeContactRidgeEvidence right
    ) =>
        TemplatePngFiles.Same(left.ObservedContactDigest, right.ObservedContactDigest)
        && left.ObservedArea == right.ObservedArea
        && left.RidgeExteriorArea == right.RidgeExteriorArea
        && left.LargestRidgeExteriorArea == right.LargestRidgeExteriorArea;

    internal static UnitResult<DesktopError> Check(
        TemplateContourManifest manifest,
        Fs50ContactContourArtifact contour,
        ImmutableArray<byte> observed
    )
    {
        var evidence = manifest.RidgeEvidence;
        if (
            observed.Length != 600000
            || evidence is null
            || !TemplatePngFiles.Same(
                evidence.ObservedContactDigest,
                manifest.Planes.ObservedContact.PixelSha256
            )
            || evidence.ObservedArea <= 0
            || evidence.ObservedArea > 600000
            || evidence.RidgeExteriorArea < 0
            || evidence.RidgeExteriorArea > evidence.ObservedArea
            || evidence.LargestRidgeExteriorArea < 0
            || evidence.LargestRidgeExteriorArea > evidence.RidgeExteriorArea
        )
            return Invalid();
        var area = 0;
        for (var i = 0; i < observed.Length; i++)
        {
            if (observed[i] > 1 || contour.ContactMask[i] > observed[i])
                return Invalid();
            area += observed[i];
        }
        return area == evidence.ObservedArea ? UnitResult.Success<DesktopError>() : Invalid();
    }

    private static DesktopError Invalid() =>
        TemplatePngFiles.Invalid(
            "Observed whole-contact texture contradicts its plane, contour, or exterior witness."
        );
}
