// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Cli.Settings;

using System;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Shared reader and explicit guided-capture options.</summary>
public class FingerprintCaptureSettings : CredentialInputSettings
{
    /// <summary>Explicit approved scanner identity.</summary>
    [CommandOption("--capture-device <ID>")]
    [Description(
        "Explicit capture device identity with an authenticated acquisition-policy mapping."
    )]
    public string CaptureDevice { get; set; } = "";

    /// <summary>Enrolled INCITS finger position, when explicitly selected.</summary>
    [CommandOption("--finger <POSITION>")]
    [Description(
        "Optional enrolled INCITS finger position, 1–10. Interactive mode otherwise uses the first authenticated choice."
    )]
    public byte? Finger { get; set; }

    /// <summary>Enrolled reference view number.</summary>
    [CommandOption("--view <NUMBER>")]
    [Description("Enrolled reference view, defaults to zero.")]
    public byte View { get; set; }

    /// <summary>Explicit confirmation that the selected finger is placed.</summary>
    [CommandOption("--capture-ready")]
    [Description(
        "Explicitly confirm the selected finger is already placed; requires --finger and avoids capture prompts."
    )]
    public bool CaptureReady { get; set; }

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        var common = base.Validate();
        if (!common.Successful)
            return common;
        return
            string.IsNullOrWhiteSpace(Reader)
            || string.IsNullOrWhiteSpace(CaptureDevice)
            || Finger.HasValue && Finger.Value is < 1 or > 10
            || View > 15
            || CaptureReady && !Finger.HasValue
            ? ValidationResult.Error(
                "Specify a reader and capture device; capture-ready requires an enrolled finger position."
            )
            : ValidationResult.Success();
    }
}
