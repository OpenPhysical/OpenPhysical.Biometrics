// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

// Writes an absent value as JSON null and reads null or a missing member as absent.
internal sealed class MaybeJsonConverter<T> : JsonConverter<Maybe<T>>
    where T : notnull
{
    public override bool HandleNull => true;

    public override Maybe<T> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Maybe<T>.None;
        return JsonSerializer.Deserialize<T>(ref reader, options) is { } value
            ? Maybe.From(value)
            : Maybe<T>.None;
    }

    public override void Write(Utf8JsonWriter writer, Maybe<T> value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            JsonSerializer.Serialize(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}
