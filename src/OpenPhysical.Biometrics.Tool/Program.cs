// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Tool.Commands;

// Record inspection is shared with library and middleware callers. Each command
// chooses its renderer while retaining the same parsing and profile findings.
if (args is ["face", "prepare", .. var facePreparation])
    return FacePreparationCommand.Run(facePreparation);
if (args is ["inspect", .. var inspection])
    return BiometricInspectionCommand.Run(inspection);
if (args is ["convert", .. var conversion])
    return FingerprintConversionCommand.Run(conversion);
if (args is ["validate", .. var validation])
    return BiometricInspectionCommand.Run(validation, requireProfile: true);

Console.WriteLine(
    "biotool inspect <raw-record> [--json] [--include-sensitive] [--at <UTC timestamp>]"
);
Console.WriteLine("biotool validate <raw-record> [--json] [--at <UTC timestamp>]");
Console.WriteLine(
    "biotool face prepare <image> --output <portrait.jp2> [--preview <portrait.png>] [--json]"
);
Console.WriteLine("biotool convert <incits378.fmr> <output.json> --finger N --view N");
return args.Length == 0 || args is ["--help"] ? 0 : 3;
