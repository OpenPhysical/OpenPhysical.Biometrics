// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class TemplateExtractorMetadata
{
    internal static JsonElement Write(Maybe<string> value) =>
        value.Match(identity => JsonSerializer.SerializeToElement(identity), () => default);

    internal static Result<Maybe<string>, DesktopError> Read(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? Maybe<string>.None
        : value.ValueKind == JsonValueKind.String
        && value.GetString() is { } identity
        && !string.IsNullOrWhiteSpace(identity)
            ? Maybe.From(identity)
        : TemplatePngFiles.Invalid("The saved extractor identity must be a nonblank string.");
}
