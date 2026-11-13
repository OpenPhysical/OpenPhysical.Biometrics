// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A transient operator notice without file paths or parser diagnostics.</summary>
/// <param name="Message">Brief operator-facing explanation.</param>
/// <param name="IsError">Whether the requested operation could not complete.</param>
public sealed record SettingsNotice(string Message, bool IsError);
