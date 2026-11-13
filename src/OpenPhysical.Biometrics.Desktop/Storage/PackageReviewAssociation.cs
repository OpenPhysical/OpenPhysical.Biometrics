// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

internal static class PackageReviewAssociation
{
    internal static UnitResult<DesktopError> Check(
        BiometricPackage package,
        bool face,
        bool fingerprints
    ) =>
        DesktopEdge
            .Try(
                () => CheckEvidence(package, face, fingerprints),
                "Package.Objects",
                "The package capture plan is invalid."
            )
            .Bind(value => value);

    private static UnitResult<DesktopError> CheckEvidence(
        BiometricPackage package,
        bool face,
        bool fingerprints
    )
    {
        using var json = JsonDocument.Parse(package.EvidenceJson.AsMemory());
        if (!json.RootElement.TryGetProperty("Sections", out var sections))
            return Invalid();
        if (sections.ValueKind != JsonValueKind.Array || sections.GetArrayLength() is < 1 or > 2)
            return Invalid();
        var expected = sections
            .EnumerateArray()
            .Select(value =>
                value.ValueKind == JsonValueKind.String ? value.GetString() : string.Empty
            )
            .ToArray();
        return
            expected.Distinct(StringComparer.Ordinal).Count() == expected.Length
            && expected.All(value => value is "face" or "fingerprints")
            && expected.Contains("face") == face
            && expected.Contains("fingerprints") == fingerprints
            ? UnitResult.Success<DesktopError>()
            : Invalid();
    }

    private static DesktopError Invalid() =>
        new("Package.Objects", "The saved objects do not match the person's capture plan.");
}
