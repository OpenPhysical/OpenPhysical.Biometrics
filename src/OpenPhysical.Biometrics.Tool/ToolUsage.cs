// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool;

using CSharpFunctionalExtensions;

internal static class ToolUsage
{
    internal static Result<CommandOutcome, ToolError> Show(string[] args) =>
        CommandOutcome
            .Of(
                args.Length == 0 || args is ["--help"] ? 0 : 3,
                "biotool inspect <raw-record> [--json] [--include-sensitive] [--at <UTC timestamp>]",
                "biotool validate <raw-record> [--json] [--at <UTC timestamp>]",
                "biotool face prepare <image> --output <portrait.jp2> [--preview <portrait.png>] [--json]",
                "biotool convert <incits378.fmr> <output.json> --finger N --view N"
            )
            .Succeed();
}
