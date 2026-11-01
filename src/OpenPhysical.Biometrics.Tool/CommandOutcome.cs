// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

using System.Collections.Immutable;

// The completed result of a command: the exit code and the lines it presents.
internal sealed record CommandOutcome(int ExitCode, ImmutableArray<ToolLine> Lines)
{
    internal static CommandOutcome Of(int exitCode, params string[] lines) =>
        new(exitCode, [.. lines.Select(line => new ToolLine(line))]);
}
