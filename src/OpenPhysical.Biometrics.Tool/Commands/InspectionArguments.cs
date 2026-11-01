// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Globalization;
using CSharpFunctionalExtensions;

internal sealed record InspectionArguments(
    string Path,
    bool Json,
    bool Sensitive,
    DateTimeOffset At
)
{
    private const string Usage =
        "biotool inspect <raw-record> [--json] [--include-sensitive] [--at <UTC timestamp>]";

    internal static Result<InspectionArguments, ToolError> Parse(string[] args)
    {
        if (args.Length == 0)
            return ToolErrors.Usage(Usage).Fail<InspectionArguments>();
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
                    return ToolErrors
                        .Usage(
                            "Unknown option or invalid timestamp. Use an ISO 8601 round-trip timestamp."
                        )
                        .Fail<InspectionArguments>();
            }
        }
        return new InspectionArguments(args[0], json, sensitive, at).Succeed();
    }
}
