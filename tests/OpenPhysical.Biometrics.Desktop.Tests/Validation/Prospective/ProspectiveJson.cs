// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

internal static class ProspectiveJson
{
    internal static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var result = new JsonSerializerOptions { WriteIndented = true };
        result.Converters.Add(new OptionalScoreConverter());
        return result;
    }
}

internal sealed class OptionalScoreConverter : JsonConverter<Maybe<int>>
{
    public override Maybe<int> Read(
        ref Utf8JsonReader reader,
        Type type,
        JsonSerializerOptions options
    ) => reader.TokenType == JsonTokenType.Null ? Maybe<int>.None : Maybe.From(reader.GetInt32());

    public override void Write(
        Utf8JsonWriter writer,
        Maybe<int> value,
        JsonSerializerOptions options
    )
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
