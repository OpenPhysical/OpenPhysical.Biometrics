// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

internal sealed class MaybeJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Maybe<>);

    public override JsonConverter? CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        Activator.CreateInstance(
            typeof(MaybeJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments())
        ) as JsonConverter;
}
