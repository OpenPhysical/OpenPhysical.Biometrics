// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

// A failed command. The message is the exact diagnostic written to standard error.
internal sealed record ToolError(string Code, string Message);
