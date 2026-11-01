// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Tool.Commands;

// The one place where a command's final result becomes console output and an exit code.
internal static class CommandRunner
{
    private const int FailureExitCode = 1;
    private const int UsageExitCode = 3;

    internal static int Run(Result<CommandOutcome, ToolError> result, bool failureAsJson = false) =>
        result.IsSuccess ? Present(result.Value) : Present(result.Error, failureAsJson);

    private static int Present(CommandOutcome outcome)
    {
        foreach (var line in outcome.Lines)
            Write(line);
        return outcome.ExitCode;
    }

    private static int Present(ToolError error, bool asJson)
    {
        Write(
            asJson
                ? new ToolLine(
                    JsonSerializer.Serialize(
                        new { Status = "Failed", error.Code },
                        FaceJson.Options
                    )
                )
                : new ToolLine(error.Message, true)
        );
        return error.Code == ToolErrorCodes.Usage ? UsageExitCode : FailureExitCode;
    }

    private static void Write(ToolLine line) =>
        (line.IsError ? Console.Error : Console.Out).WriteLine(line.Text);
}
