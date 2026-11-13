// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Cli.Commands;

using System;
using System.ComponentModel;
using OpenPhysical.Middleware.Cli.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Fingerprint capture settings.</summary>
public sealed class TwicVerifySettings : FingerprintCaptureSettings
{
    /// <summary>Exact server-signed TWIC workflow profile.</summary>
    [CommandOption("--profile <ID>")]
    [Description("Exact TWIC workflow profile from the server-signed bundle.")]
    public string Profile { get; set; } = "";

    /// <summary>Current MARSEC level for cancellation freshness checks.</summary>
    [CommandOption("--marsec <LEVEL>")]
    [Description("Required current MARSEC level, 1–3, for cancellation-list freshness policy.")]
    public int? MarsecLevel { get; set; }

    /// <summary>Separately acquired TWIC privacy-key input file.</summary>
    [CommandOption("--privacy-key <PATH>")]
    [Description(
        "Optional local TPK container file; required for contactless. Contact mode otherwise reads the card."
    )]
    public string PrivacyKeyPath { get; set; } = "";

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        var common = base.Validate();
        if (!common.Successful)
            return common;
        return
            string.IsNullOrWhiteSpace(Profile)
            || Profile.Length > 256
            || MarsecLevel is not (>= 1 and <= 3)
            || Interface == "contactless" && string.IsNullOrWhiteSpace(PrivacyKeyPath)
            ? ValidationResult.Error(
                "Select a signed TWIC profile and MARSEC level 1–3; contactless requires a separately acquired privacy key."
            )
            : ValidationResult.Success();
    }
}
