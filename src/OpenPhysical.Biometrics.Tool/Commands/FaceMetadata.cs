// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Text.Json;
using CSharpFunctionalExtensions;

// Reads the small JSON documents that accompany a capture.
internal static class FaceMetadata
{
    private const long Limit = 64 * 1024;

    internal static Result<Maybe<T>, ToolError> ReadOptional<T>(Maybe<string> path)
        where T : class =>
        path.HasNoValue
            ? Maybe<T>.None.Succeed()
            : Read<T>(path.Value).Map(value => Maybe.From(value));

    internal static Result<T, ToolError> Read<T>(string path)
        where T : class =>
        ToolEdge
            .Try(() => new FileInfo(path).Length, ToolErrors.FaceFileAccess())
            .Ensure(length => length <= Limit, ToolErrors.FaceInput())
            .Bind(_ => ToolEdge.Try(() => File.ReadAllBytes(path), ToolErrors.FaceFileAccess()))
            .Bind(bytes =>
                ToolEdge.Try(
                    () => JsonSerializer.Deserialize<T>(bytes, FaceJson.Options),
                    ToolErrors.FaceMetadata()
                )
            )
            .Bind(value => Maybe.From(value).ToResult(ToolErrors.FaceInput()));
}
