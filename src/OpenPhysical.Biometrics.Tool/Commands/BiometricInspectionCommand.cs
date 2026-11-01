// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats;
using OpenPhysical.Biometrics.Piv;

// This path uses the canonical codecs directly. Inspecting a record therefore
// performs the same interpretation as library and middleware consumers.
internal static class BiometricInspectionCommand
{
    private const long InputLimit = 64 * 1024 * 1024;

    internal static Result<CommandOutcome, ToolError> Run(
        string[] args,
        bool requireProfile = false
    ) => InspectionArguments.Parse(args).Bind(arguments => Inspect(arguments, requireProfile));

    private static Result<CommandOutcome, ToolError> Inspect(
        InspectionArguments arguments,
        bool requireProfile
    ) =>
        ToolEdge
            .Try(() => new FileInfo(arguments.Path).Length, ToolErrors.InspectionReadFailed())
            .Ensure(length => length <= InputLimit, ToolErrors.InspectionTooLarge())
            .Bind(_ =>
                ToolEdge.Try(
                    () => ReadAndRender(arguments, requireProfile),
                    ToolErrors.InspectionReadFailed()
                )
            );

    private static CommandOutcome ReadAndRender(
        InspectionArguments arguments,
        bool requireProfile
    ) =>
        SensitiveBytes.With(
            File.ReadAllBytes(arguments.Path),
            bytes =>
                InspectionRenderer.Render(
                    BiometricInspector.Inspect(
                        bytes,
                        arguments.At,
                        new InspectionOptions(arguments.Sensitive)
                    ),
                    arguments,
                    requireProfile
                )
        );
}
