// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhysical.Biometrics.Formats;
using OpenPhysical.Biometrics.Piv;

// This path uses the canonical codecs directly. Inspecting a record therefore
// performs the same interpretation as library and middleware consumers.
internal static class BiometricInspectionCommand
{
    internal static int Run(string[] args, bool requireProfile = false)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "biotool inspect <raw-record> [--json] [--include-sensitive] [--at <UTC timestamp>]"
            );
            return 3;
        }
        var json = false;
        var sensitive = false;
        var at = DateTimeOffset.UtcNow;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--include-sensitive":
                    sensitive = true;
                    break;
                case "--at"
                    when i + 1 < args.Length
                        && DateTimeOffset.TryParseExact(
                            args[i + 1],
                            "O",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var timestamp
                        ):
                    at = timestamp;
                    i++;
                    break;
                default:
                    Console.Error.WriteLine(
                        "Unknown option or invalid timestamp. Use an ISO 8601 round-trip timestamp."
                    );
                    return 3;
            }
        }
        byte[]? bytes = null;
        try
        {
            if (new FileInfo(args[0]).Length > 64 * 1024 * 1024)
            {
                Console.Error.WriteLine("The record exceeds the inspection input limit.");
                return 1;
            }
            bytes = File.ReadAllBytes(args[0]);
            var report = BiometricInspector.Inspect(bytes, at, new InspectionOptions(sensitive));
            if (json)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                options.Converters.Add(new JsonStringEnumConverter());
                Console.WriteLine(JsonSerializer.Serialize(report, options));
            }
            else
            {
                Console.WriteLine($"Record: {report.Kind}");
                Console.WriteLine(
                    $"Finger views: {report.FingerViews.Length}; facial images: {report.FacialImages.Length}"
                );
                Console.WriteLine(
                    $"Structure findings: {report.Structure.Diagnostics.Length}; PIV profile findings: {report.ProfileDiagnostics.Length}"
                );
                Console.WriteLine(
                    $"Detached signature block: {(report.HasSignatureBlock ? "present" : "absent")}"
                );
                foreach (var diagnostic in report.Structure.Diagnostics)
                    Console.WriteLine(
                        $"{diagnostic.Code} at byte {diagnostic.Offset}, length {diagnostic.Length}"
                    );
                foreach (var diagnostic in report.ProfileDiagnostics)
                    Console.WriteLine($"{diagnostic.Code}: {diagnostic.Field}");
                if (sensitive)
                    foreach (
                        var field in report.Structure.Fields.Where(field => field.Value.HasValue)
                    )
                        Console.WriteLine(
                            $"{field.Name} [{field.Offset}:{field.Length}]: {field.Value.Value}"
                        );
            }
            if (!report.Structure.Diagnostics.IsEmpty)
                return 1;
            return requireProfile && !report.ProfileDiagnostics.IsEmpty ? 2 : 0;
        }
        catch (Exception error)
            when (error
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            Console.Error.WriteLine("Reading or interpreting the input record failed.");
            return 1;
        }
        finally
        {
            if (bytes is not null)
                Array.Clear(bytes);
        }
    }
}
