// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhysical.Biometrics.Piv;

// Presents one inspection report as JSON or as the summary lines, and chooses the exit code.
internal static class InspectionRenderer
{
    internal static CommandOutcome Render(
        BiometricInspectionReport report,
        InspectionArguments arguments,
        bool requireProfile
    )
    {
        var lines = arguments.Json ? Json(report) : Text(report, arguments.Sensitive);
        return new CommandOutcome(
            ExitCode(report, requireProfile),
            [.. lines.Select(line => new ToolLine(line))]
        );
    }

    private static int ExitCode(BiometricInspectionReport report, bool requireProfile)
    {
        if (!report.Structure.Diagnostics.IsEmpty)
            return 1;
        return requireProfile && !report.ProfileDiagnostics.IsEmpty ? 2 : 0;
    }

    private static ImmutableArray<string> Json(BiometricInspectionReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return [JsonSerializer.Serialize(report, options)];
    }

    private static IEnumerable<string> Text(BiometricInspectionReport report, bool sensitive)
    {
        yield return $"Record: {report.Kind}";
        yield return $"Finger views: {report.FingerViews.Length}; facial images: {report.FacialImages.Length}";
        yield return $"Structure findings: {report.Structure.Diagnostics.Length}; PIV profile findings: {report.ProfileDiagnostics.Length}";
        yield return $"Detached signature block: {(report.HasSignatureBlock ? "present" : "absent")}";
        foreach (var diagnostic in report.Structure.Diagnostics)
            yield return $"{diagnostic.Code} at byte {diagnostic.Offset}, length {diagnostic.Length}";
        foreach (var diagnostic in report.ProfileDiagnostics)
            yield return $"{diagnostic.Code}: {diagnostic.Field}";
        if (sensitive)
            foreach (var field in report.Structure.Fields.Where(field => field.Value.HasValue))
                yield return $"{field.Name} [{field.Offset}:{field.Length}]: {field.Value.Value}";
    }
}
