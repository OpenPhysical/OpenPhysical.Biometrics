// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

// One line destined for standard output, or standard error when IsError is set.
internal sealed record ToolLine(string Text, bool IsError = false);
