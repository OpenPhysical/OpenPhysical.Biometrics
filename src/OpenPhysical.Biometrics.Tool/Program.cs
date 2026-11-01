// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Tool;
using OpenPhysical.Biometrics.Tool.Commands;

// Record inspection is shared with library and middleware callers. Each command
// chooses its renderer while retaining the same parsing and profile findings.
return args switch
{
    ["face", "prepare", .. var facePreparation] => CommandRunner.Run(
        FacePreparationCommand.Run(facePreparation),
        failureAsJson: facePreparation.Contains("--json")
    ),
    ["inspect", .. var inspection] => CommandRunner.Run(BiometricInspectionCommand.Run(inspection)),
    ["convert", .. var conversion] => CommandRunner.Run(
        FingerprintConversionCommand.Run(conversion)
    ),
    ["validate", .. var validation] => CommandRunner.Run(
        BiometricInspectionCommand.Run(validation, requireProfile: true)
    ),
    _ => CommandRunner.Run(ToolUsage.Show(args)),
};
