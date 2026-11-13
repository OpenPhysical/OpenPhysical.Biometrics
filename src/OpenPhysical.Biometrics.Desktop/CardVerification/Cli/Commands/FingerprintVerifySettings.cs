// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Cli.Commands;

using System;
using System.ComponentModel;
using System.Linq;
using OpenPhysical.Middleware.Cli.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Fingerprint capture settings.</summary>
public sealed class FingerprintVerifySettings : FingerprintCaptureSettings
{
    /// <summary>Leaf-first content signer certificate path.</summary>
    [CommandOption("--content-signer <PATH>")]
    [Description(
        "DER content-signing certificate path, repeated in leaf-first order through the root."
    )]
    public string[] ContentSignerPaths { get; set; } = [];

    /// <summary>Read the protected-reference PIN from standard input.</summary>
    [CommandOption("--pin-stdin")]
    [Description("Read one PIN from redirected standard input for this card operation.")]
    public bool PinStdin { get; set; }

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        var common = base.Validate();
        if (!common.Successful)
            return common;
        return
            ContentSignerPaths is not { Length: >= 2 and <= 10 }
            || ContentSignerPaths.Any(string.IsNullOrWhiteSpace)
            ? ValidationResult.Error(
                "Specify a reader, capture device and leaf-first content-signing path; capture-ready requires an enrolled finger position."
            )
            : ValidationResult.Success();
    }
}
