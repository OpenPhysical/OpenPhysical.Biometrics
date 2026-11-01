// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

/// <summary>Serializes an absent optional text value as JSON null and restores it as no value.</summary>
public sealed class OptionalTextJsonConverter : JsonConverter<Maybe<string>>
{
    /// <summary>Reads JSON null as no value and a JSON string as its text.</summary>
    /// <param name="reader">The JSON reader positioned at the value.</param>
    /// <param name="typeToConvert">The requested optional-text type.</param>
    /// <param name="options">The active serializer options.</param>
    /// <returns>The optional text.</returns>
    public override Maybe<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        reader.TokenType == JsonTokenType.String
            ? Maybe.From(reader.GetString() ?? string.Empty)
            : Maybe<string>.None;

    /// <summary>Writes the text, or JSON null when no value exists.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The optional text.</param>
    /// <param name="options">The active serializer options.</param>
    public override void Write(
        Utf8JsonWriter writer,
        Maybe<string> value,
        JsonSerializerOptions options
    ) => value.Match(text => writer.WriteStringValue(text), () => writer.WriteNullValue());
}
