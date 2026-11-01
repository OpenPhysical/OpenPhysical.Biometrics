// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

// Presents a preparation outcome as lines of JSON or text.
internal static class FaceReport
{
    internal static Result<ImmutableArray<ToolLine>, ToolError> Lines(
        PortraitPreparationResult result,
        FaceArguments arguments
    ) => ToolEdge.Try(() => Render(result, arguments), ToolErrors.FacePreparation());

    private static ImmutableArray<ToolLine> Render(
        PortraitPreparationResult result,
        FaceArguments arguments
    )
    {
        var summary = Serialize(Summary(result, arguments.IncludeSensitive));
        if (arguments.Json)
            return [new ToolLine(summary)];
        var lines = ImmutableArray.CreateBuilder<ToolLine>();
        lines.Add(new ToolLine($"{result.Status}: {result.Faces.Length} face(s)."));
        lines.AddRange(
            result.Findings.Select(finding => new ToolLine($"{finding.Code}: {finding.Message}"))
        );
        if (arguments.IncludeSensitive)
            lines.Add(new ToolLine(summary));
        return lines.ToImmutable();
    }

    private static string Serialize(object summary) =>
        JsonSerializer.Serialize(summary, FaceJson.Options);

    private static object Summary(PortraitPreparationResult result, bool sensitive) =>
        sensitive
            ? new
            {
                result.Status,
                ModelRevision = PortraitProvenance.ModelRevision,
                GeometryPolicy = PortraitProvenance.GeometryPolicy,
                result.SourceSha256,
                result.SourceWidth,
                result.SourceHeight,
                result.Faces,
                result.Transform,
                result.Measurements,
                result.Findings,
            }
            : new
            {
                result.Status,
                FaceCount = result.Faces.Length,
                Width = 480,
                Height = 640,
                Findings = result.Findings.Select(f => f.Code).ToArray(),
            };
}
