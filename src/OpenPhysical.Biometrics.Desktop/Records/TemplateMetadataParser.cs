// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Globalization;
using CSharpFunctionalExtensions;

internal static class TemplateMetadataParser
{
    internal static Result<TemplateMetadata, DesktopError> Parse(
        string owner,
        string productType,
        string equipment
    ) =>
        Number(owner, "Product Owner ID")
            .Bind(productOwner =>
                Number(productType, "Product Type")
                    .Bind(type =>
                        Number(equipment, "Capture Equipment ID")
                            .Map(id => new TemplateMetadata(productOwner, type, id))
                    )
            );

    private static Result<ushort, DesktopError> Number(string text, string field)
    {
        var hexadecimal = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        return ushort.TryParse(
            hexadecimal ? text[2..] : text,
            hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value
        )
            ? value
            : new DesktopError(
                "InvalidMetadata",
                $"Enter a valid {field} (decimal or 0x hexadecimal)."
            );
    }
}
