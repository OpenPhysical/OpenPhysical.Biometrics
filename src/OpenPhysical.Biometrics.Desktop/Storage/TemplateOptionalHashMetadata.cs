// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class TemplateOptionalHashMetadata
{
    internal static JsonElement Write(Maybe<string> value) =>
        value.Match(hash => JsonSerializer.SerializeToElement(hash), Empty);

    internal static JsonElement Empty()
    {
        using var document = JsonDocument.Parse("null");
        return document.RootElement.Clone();
    }

    internal static Result<Maybe<string>, DesktopError> Read(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? Maybe<string>.None
        : value.ValueKind == JsonValueKind.String
        && value.GetString() is { } hash
        && TemplatePngFiles.Hash(hash)
            ? Maybe.From(hash)
        : TemplatePngFiles.Invalid(
            "An optional qualification hash must be explicit null or SHA-256."
        );
}
